﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using stock_market.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.Xml;
using System.Security.Policy;
using System.Threading.Tasks;

public class DbInitializer : IDbInitializer
{
    private readonly IServiceScopeFactory _scopeFactory;

    private List<string> tickers = new List<string>();

    private const string date_from = "2022-10-15";
    private const string URL = "https://api.marketstack.com/v1/intraday";
    private const string parameters_before_sym = "?access_key=cb0917fe9d7a54323143c281fa427aa2&symbols=";
    private const string parameters_after_sym = "&interval=1min&date_from=" + date_from + "&date_to=";


    public DbInitializer(IServiceScopeFactory scopeFactory)
    {
        this._scopeFactory = scopeFactory;
    }

    public void Initialize()
    {
        using (var serviceScope = _scopeFactory.CreateScope())
        {
            using (var context = serviceScope.ServiceProvider.GetService<mainDB>())
            {
                context.Database.Migrate();
            }
        }
    }

    public void InitializeTimestamps()
    {
        using (var serviceScope = _scopeFactory.CreateScope())
        {
            using (var _db = serviceScope.ServiceProvider.GetService<mainDB>())
            {
                // Create a Timestamp object for every minute since date_from
                DateTime date = DateTime.ParseExact(date_from, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                DateTime now = DateTime.Now;
                List<Timestamp> all_timestamps = new List<Timestamp>();
                while (date < now)
                {
                    date = date.AddMinutes(1);
                    Timestamp timestamp = new Timestamp();
                    timestamp.time = date;
                    timestamp.unix = (int)date.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                    all_timestamps.Add(timestamp);
                }
                _db.timestamps.AddRange(all_timestamps);
                _db.SaveChanges();
                Console.WriteLine("Timestamps added");
            }
        }
    }

    public void InitializeUser()
    {
        using (var serviceScope = _scopeFactory.CreateScope())
        {
            using (var _db = serviceScope.ServiceProvider.GetService<mainDB>())
            {
                //Create User
                User john_doe = new User();
                john_doe.curr_balance = 100_000;
                john_doe.curr_balance_liquid = 100_000;
                john_doe.curr_balance_stock = 0;
                john_doe.first_name = "John";
                john_doe.last_name = "Doe";
                _db.Users.Add(john_doe);

                _db.SaveChanges();
            }
        }
    }

    public void InitializeStocks()
    {
        using (var serviceScope = _scopeFactory.CreateScope())
        {
            using (var _db = serviceScope.ServiceProvider.GetService<mainDB>())
            {

                tickers.Add("TSLA");
                tickers.Add("AMZN");
                tickers.Add("AAPL");

                List<HistoricalStock> all_historical_stocks = new List<HistoricalStock>();
                List<Timestamp> all_timestamps = _db.timestamps.ToList();
                List<BaseStock> all_base_stocks = new List<BaseStock>();

                foreach (var tick in tickers)
                {
                    HttpClient client = new HttpClient();
                    client.BaseAddress = new Uri(URL);
                    string date_to = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                    client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
                    string param = parameters_before_sym + tick + parameters_after_sym + date_to;
                    HttpResponseMessage response = client.GetAsync(param).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        var body = response.Content.ReadAsStringAsync().Result;
                        Root api_returns = JsonConvert.DeserializeObject<Root>(body);

                        int total_pages = (int)Math.Ceiling((double)api_returns.pagination.total / 10000);
                        Console.WriteLine("Total pages: " + total_pages);
                        Console.WriteLine("Total entries: " + api_returns.pagination.total);

                        //for each page, get the data and add it to the database
                        for (int i = 0; i < total_pages; i++)
                        {
                            //get the data
                            param = parameters_before_sym + tick + parameters_after_sym + date_to + "&limit=10000&offset=" + i;
                            Console.WriteLine("USING: " + param);
                            response = client.GetAsync(param).Result;
                            body = response.Content.ReadAsStringAsync().Result;
                            api_returns = JsonConvert.DeserializeObject<Root>(body);

                            foreach (var j in api_returns.data)
                            {
                                HistoricalStock ex_stock = new HistoricalStock();

                                //if j.last is null, use open price as ex_stock.price, otherwise, use last price
                                if (j.last == null)
                                {
                                    ex_stock.price = (double)j.open;
                                }
                                else
                                {
                                    ex_stock.price = Convert.ToDouble(j.last, CultureInfo.InvariantCulture);
                                }
                                
                                ex_stock.price = Math.Round(ex_stock.price, 3);
                                Timestamp ts = all_timestamps.Find(t => t.time == j.date);
                                ex_stock.timestamp = ts;

                                // find the base stock, if it doesn't exist, create it
                                BaseStock base_stock = all_base_stocks.Find(b => b.ticker == tick);
                                if (base_stock == null)
                                {
                                    base_stock = new BaseStock
                                    {
                                        ticker = tick,
                                        current_price = ex_stock.price
                                    };
                                    all_base_stocks.Add(base_stock);
                                }
                                ex_stock.baseStock = base_stock;
                                all_historical_stocks.Add(ex_stock);
                            }
                        }
                    }
                }

                foreach (var b in all_base_stocks)
                {
                    b.current_price = all_historical_stocks.FindAll(h => h.baseStock == b).First().price;
                }

                _db.baseStocks.AddRange(all_base_stocks);
                _db.historicalStocks.AddRange(all_historical_stocks);
                _db.SaveChanges();
            }
        }

    }

    public void InitializePortfolio()
    {
        {
            using (var serviceScope = _scopeFactory.CreateScope())
            {
                using (var _db = serviceScope.ServiceProvider.GetService<mainDB>())
                {
                    Console.WriteLine("Filling portfolios");

                    //For every timestamp, create a portfolio for every user
                    List<Portfolio> all_portfolios = new List<Portfolio>();
                    foreach (var ts in _db.timestamps)
                    {
                        foreach (var user in _db.Users)
                        {
                            Portfolio portfolio = new Portfolio
                            {
                                timestamp = ts,
                                user = user,
                                total_value = user.curr_balance,
                                stock_value = 0,
                                liquid_value = user.curr_balance,
                                stock_counter = new List<BaseStockCounter>()
                            };
                            all_portfolios.Add(portfolio);
                        }
                    }
                    _db.portfolios.AddRange(all_portfolios);
                    _db.SaveChanges();
                }
            }
        }
    }


    public void FillHistoricalStocks()
    {
        using (var serviceScope = _scopeFactory.CreateScope())
        {
            using (var _db = serviceScope.ServiceProvider.GetService<mainDB>())
            {

                List<HistoricalStock> new_historical_stocks = new List<HistoricalStock>();
                List<HistoricalStock> replaced_historical_stocks = new List<HistoricalStock>();
                List<HistoricalStock> all_historical_stocks = _db.historicalStocks
                    .Include(h => h.baseStock)
                    .ToList();
                List<BaseStock> all_base_stocks = _db.baseStocks.ToList();
                List<Timestamp> all_timestamps = _db.timestamps.ToList();
                
                Timestamp latest_timestamp = _db.timestamps.OrderBy(t => t.time).Last();
                latest_timestamp.time = latest_timestamp.time.AddDays(-1);


                string date_from = latest_timestamp.time.ToString("yyyy-MM-dd");
                string URL = "https://api.marketstack.com/v1/intraday";
                string parameters_before_sym = "?access_key=cb0917fe9d7a54323143c281fa427aa2&symbols=";
                string parameters_after_sym = "&interval=1min&date_from=" + date_from + "&date_to=";
                string date_to = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                foreach (BaseStock base_stock in all_base_stocks) {
                    
                    string ticker = base_stock.ticker;

                    HttpClient client = new HttpClient();
                    client.BaseAddress = new Uri(URL);

                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));

                    string param = parameters_before_sym + ticker + parameters_after_sym + date_to;
                    HttpResponseMessage response = client.GetAsync(param).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        var body = response.Content.ReadAsStringAsync().Result;
                        Root api_returns = JsonConvert.DeserializeObject<Root>(body);
                        int total_pages = (int)Math.Ceiling((double)api_returns.pagination.total / 10000);

                        for (int i = 0; i < total_pages; i++)
                        {
                            param = parameters_before_sym + ticker + parameters_after_sym + date_to + "&limit=10000&offset=" + i;
                            Console.WriteLine("USING: " + param);
                            response = client.GetAsync(param).Result;
                            body = response.Content.ReadAsStringAsync().Result;
                            api_returns = JsonConvert.DeserializeObject<Root>(body);

                            foreach (var j in api_returns.data)
                            {
                                HistoricalStock ex_stock = new HistoricalStock();

                                //if j.last is null, use open price as ex_stock.price, otherwise, use last price
                                if (j.last == null)
                                {
                                    ex_stock.price = (double)j.open;
                                }
                                else
                                {
                                    ex_stock.price = Convert.ToDouble(j.last, CultureInfo.InvariantCulture);
                                }

                                Timestamp ts = all_timestamps.Find(t => t.time == j.date);

                                if (all_historical_stocks.Any(h => h.timestamp == ts && h.baseStock == base_stock))
                                {
                                    replaced_historical_stocks.Add(all_historical_stocks.Find(h => h.timestamp == ts && h.baseStock == base_stock));
                                }

                                ex_stock.timestamp = ts;
                                ex_stock.baseStock = base_stock;
                                new_historical_stocks.Add(ex_stock);
                            }
                        }
                    }
                }

                foreach (BaseStock base_stock in all_base_stocks)
                {

                    base_stock.current_price = new_historical_stocks.OrderByDescending(h => h.timestamp.time).First(h => h.baseStock == base_stock).price;
                }


                _db.historicalStocks.RemoveRange(replaced_historical_stocks);
                _db.SaveChanges();
                _db.historicalStocks.AddRange(new_historical_stocks);
                _db.SaveChanges();

            }
        }
    }

    public void FFillHistoricalStocks()
    {
        using (var serviceScope = _scopeFactory.CreateScope())
        {
            using (var _db = serviceScope.ServiceProvider.GetService<mainDB>())
            {
                //For every timestamp, check if every stock has a historicalStock object for that timestamp

                Console.WriteLine("Filling historical stocks");

                List<HistoricalStock> all_ffilled_historical_stocks = new List<HistoricalStock>();
                List<HistoricalStock> all_historical_stocks = _db.historicalStocks.ToList();
                List<Timestamp> all_timestamps = _db.timestamps.ToList();
                List<BaseStock> all_base_stocks = _db.baseStocks.ToList();
                all_timestamps.Sort((x, y) => x.unix.CompareTo(y.unix));

                foreach (var ts in all_timestamps)
                {
                    foreach (var base_stock in all_base_stocks)
                    {
                        //check if the historical stock exists in the list of historical stocks
                        if (!all_historical_stocks.Any(h => h.timestamp == ts && h.baseStock == base_stock))
                        {
                            HistoricalStock prev_hs = all_historical_stocks.Find(h => h.baseStock == base_stock && h.timestamp.unix == ts.unix - 60);

                            if (prev_hs != null)
                            {
                                HistoricalStock historical_stock = new HistoricalStock();
                                historical_stock.baseStock = base_stock;
                                historical_stock.timestamp = ts;
                                historical_stock.price = prev_hs.price;
                                all_ffilled_historical_stocks.Add(historical_stock);
                                all_historical_stocks.Add(historical_stock);
                            }
                        }
                    }
                }
                _db.historicalStocks.AddRange(all_ffilled_historical_stocks);
                _db.SaveChanges();
            }
        }
    }

    public void FFillHistoricalStocks(DateTime from)
    {
        using (var serviceScope = _scopeFactory.CreateScope())
        {
            using (var _db = serviceScope.ServiceProvider.GetService<mainDB>())
            {
                //For every timestamp, check if every stock has a historicalStock object for that timestamp

                //Console.WriteLine("Filling historical stocks from " + from);

                List<HistoricalStock> all_ffilled_historical_stocks = new List<HistoricalStock>();

                List<HistoricalStock> all_historical_stocks = _db.historicalStocks.ToList();

                List<Timestamp> all_timestamps = _db.timestamps.Where(t => t.time >= from.AddMinutes(-30)).ToList();
                List<BaseStock> all_base_stocks = _db.baseStocks.ToList();
                all_timestamps.Sort((x, y) => x.unix.CompareTo(y.unix));

                
                foreach (var ts in all_timestamps)
                {
                    foreach (var base_stock in all_base_stocks)
                    {
                        //check if the historical stock exists in the list of historical stocks
                        if (!all_historical_stocks.Any(h => h.timestamp == ts && h.baseStock == base_stock))
                        {

                            Timestamp time = all_timestamps.Find(t => t.time == ts.time.AddMinutes(-1));
                            //Console.WriteLine("TEST: " + time);
                            //Console.WriteLine("Tried to find: " + time);
                            HistoricalStock prev_hs = all_historical_stocks.Find(h => h.baseStock == base_stock && h.timestamp == time);

                            if (prev_hs != null)
                            {

                                HistoricalStock historical_stock = new HistoricalStock();
                                historical_stock.baseStock = base_stock;
                                historical_stock.timestamp = ts;
                                historical_stock.price = prev_hs.price;
                                all_ffilled_historical_stocks.Add(historical_stock);
                                all_historical_stocks.Add(historical_stock);

                            }
                        }
                    }
                }
                _db.historicalStocks.AddRange(all_ffilled_historical_stocks);
                _db.SaveChanges();
            }
        }
    }

    public void FillTimestamps(DateTime latest_db, DateTime now)
    {
        using (var serviceScope = _scopeFactory.CreateScope())
        {
            using (var _db = serviceScope.ServiceProvider.GetService<mainDB>())
            {

                Console.WriteLine("Filling timestamps");
                List<Timestamp> all_timestamps = new List<Timestamp>();

                while (latest_db < now)
                {
                    latest_db = latest_db.AddMinutes(1);
                    Timestamp timestamp = new Timestamp
                    {
                        time = latest_db,
                        unix = (int)latest_db.Subtract(new DateTime(1970, 1, 1)).TotalSeconds
                    };
                    all_timestamps.Add(timestamp);
                }
                _db.timestamps.AddRange(all_timestamps);
                _db.SaveChanges();
                Console.WriteLine("Timestamps added");
            }
        }
    }

    public void FillPortfolio(DateTime latest_timestamp)
    {
        {
            using (var serviceScope = _scopeFactory.CreateScope())
            {
                using (var _db = serviceScope.ServiceProvider.GetService<mainDB>())
                {
                    List<Portfolio> all_filled_portfolios = new List<Portfolio>();

                    List<Portfolio> all_portfolios = _db.portfolios.Include(p => p.stock_counter)
                        .ThenInclude(p => p.historical)
                        .ThenInclude(p => p.baseStock)
                        .Include(p => p.timestamp)
                        .ToList();

                    List<Timestamp> all_timestamps = _db.timestamps.Where(t => t.time >= latest_timestamp.AddMinutes(-30)).ToList();
                    List<HistoricalStock> all_historical_stocks = _db.historicalStocks
                        .Include(h => h.baseStock)
                        .Include(h => h.timestamp)
                        .Where(h => h.timestamp.time >= latest_timestamp.AddMinutes(-30)).ToList();
                    
                    List<User> all_users = _db.Users.ToList();
                    all_timestamps.Sort((x, y) => x.unix.CompareTo(y.unix));


                    foreach (var ts in all_timestamps)
                    {
                        foreach (var user in all_users)
                        {
                            //check if the portfolio exists in the list of portfolio stocks
                            if (!all_portfolios.Any(h => h.timestamp == ts && h.user == user))
                            {

                                Timestamp time = all_timestamps.Find(t => t.time == ts.time.AddMinutes(-1));
                                Portfolio prev_port = all_portfolios.Find(p => p.timestamp == time && p.user == user);

                                if (prev_port != null)
                                {

                                    Portfolio historical_portfolio = new Portfolio
                                    {
                                        user = user,
                                        liquid_value = prev_port.liquid_value,
                                        stock_counter = prev_port.stock_counter,
                                        stock_value = 0
                                    };

                                    List<BaseStockCounter> all_counters = new List<BaseStockCounter>();
                                    if (historical_portfolio.stock_counter != null && historical_portfolio.stock_counter.Count != 0)
                                    {
                                        foreach (var hs in historical_portfolio.stock_counter)
                                        {

                                            BaseStockCounter temp = new BaseStockCounter
                                            {
                                                count = hs.count,
                                            };

                                            HistoricalStock historical_stock = _db.historicalStocks
                                                .Include(h => h.baseStock)
                                                .Include(h => h.timestamp)
                                                .Where(h => h.baseStock == hs.historical.baseStock && h.timestamp == ts)
                                                .FirstOrDefault();

                                            temp.historical = historical_stock;

                                            historical_portfolio.stock_value += hs.historical.price * hs.count;
                                            all_counters.Add(temp);
                                        }
                                    }


                                    historical_portfolio.total_value = historical_portfolio.stock_value
                                        + historical_portfolio.liquid_value;
                                    historical_portfolio.timestamp = ts;
                                    historical_portfolio.stock_counter = all_counters;
                                    all_filled_portfolios.Add(historical_portfolio);
                                    all_portfolios.Add(historical_portfolio);
                                }
                            }
                        }
                    }

                    //Console.WriteLine(all_filled_portfolios[0].stock_counter.Count);
                    _db.portfolios.AddRange(all_filled_portfolios);
                    _db.SaveChanges();
                }
            }
        }
    }

    public void UpdateStocks()
    {
        using (var serviceScope = _scopeFactory.CreateScope())
        {
            using (var _db = serviceScope.ServiceProvider.GetService<mainDB>())
            {
                Console.WriteLine("Updating timestamps");
                //find the latest Timestamp
                Timestamp latest_ts = _db.timestamps.OrderByDescending(t => t.time).First();
                DateTime latest_date = latest_ts.time;

                // If the latest_ts.unix is not now, then fill up untill today with
                DateTime now = DateTime.Now;

                if (latest_date != now)
                {
                    //fill up the timestamps
                    FillTimestamps(latest_date, now);

                    //Fill stocks
                    FillHistoricalStocks();

                    //FFill HistoricalStocks
                    FFillHistoricalStocks(latest_date);

                    //Fill portfolio
                    FillPortfolio(latest_date);

                }
            }
        }
    }

    public async Task SeedData()
    {
        using (var serviceScope = _scopeFactory.CreateScope())
        {
            using (var _db = serviceScope.ServiceProvider.GetService<mainDB>())
            {
                if (!_db.Users.Any())
                {
                    //Seeding the DB by calling the functions under. 
                    // This runs if main.db does not exist, or if it is empty

                    //Initialize User
                    InitializeUser();

                    //Initialize Timestamps
                    InitializeTimestamps();

                    //Initialize HistoricalStocks and BaseStocks
                    InitializeStocks();

                    //Initialize Portfolio
                    InitializePortfolio();

                    //FFill HistoricalStocks
                    FFillHistoricalStocks();

                }

                //run UpdateStocks asyncronously every minute
                
                UpdateStocks();


                //run UpdateStocks syncronously every minute
                
                

                await Task.Run(async () =>
                {
                    while (true)
                    {
                        await Task.Delay(60000);
                        UpdateStocks();
                    }
                });

            }
            }
        }
    }
