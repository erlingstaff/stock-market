using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using stock_market.Controllers;
using stock_market.DAL;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace enhetstesting
{
    public class WatchlistTest
    {

        private const string _loggetInn = "loggetInn";

        private readonly Mock<IWatchlistRepository> mockRep = new Mock<IWatchlistRepository>();
        private readonly Mock<ILogger<WatchlistController>> mockLog = new Mock<ILogger<WatchlistController>>();

        private readonly Mock<HttpContext> mockHttpContext = new Mock<HttpContext>();
        private readonly MockHttpSession mockSession = new MockHttpSession();

        [Fact]
        public async Task AddStockLoggetinnOK()
        {
            var mock = new Mock<IWatchlistRepository>();
            mock.Setup(k => k.AddStock("AAPL", 1, 50, 1)).ReturnsAsync(true);
            var watchlistController = new WatchlistController(mock.Object, mockLog.Object);

            mockSession[_loggetInn] = _loggetInn;
            mockHttpContext.Setup(s => s.Session).Returns(mockSession);
            watchlistController.ControllerContext.HttpContext = mockHttpContext.Object;

            var resualt = await watchlistController.AddStock("AAPL", 1, 50) as OkObjectResult;

            Assert.Equal((int)HttpStatusCode.OK, resualt.StatusCode);
            Assert.Equal("User added to watchlist", resualt.Value);
        }

        [Fact]
        public async Task AddstockLoggetinnIkkeOK()
        {
            mockRep.Setup(k => k.AddStock("appl", 1, 50,1)).ReturnsAsync(false);

            var watchlistController = new WatchlistController(mockRep.Object, mockLog.Object);

            mockSession[_loggetInn] = _loggetInn;
            mockHttpContext.Setup(s => s.Session).Returns(mockSession);
            watchlistController.ControllerContext.HttpContext = mockHttpContext.Object;

            // Act
            var resultat = await watchlistController.AddStock("appl", 1, 50) as BadRequestObjectResult;

            // Assert 
            Assert.Equal((int)HttpStatusCode.BadRequest, resultat.StatusCode);
            Assert.Equal("Could not add to watchlist", resultat.Value);
        }

        [Fact]
        public async Task DeleteStockLoggetinnOK()
        {
            var mock = new Mock<IWatchlistRepository>();
            mock.Setup(k => k.DeleteStock(2,1)).ReturnsAsync(true);
            var watchlistController = new WatchlistController(mock.Object, mockLog.Object);

            mockSession[_loggetInn] = _loggetInn;
            mockHttpContext.Setup(s => s.Session).Returns(mockSession);
            watchlistController.ControllerContext.HttpContext = mockHttpContext.Object;

            var resualt = await watchlistController.DeleteStock(2) as OkObjectResult;

            Assert.Equal((int)HttpStatusCode.OK, resualt.StatusCode);
            Assert.Equal("User deleted from watchlist", resualt.Value);
        }

        [Fact]
        public async Task DeletestockLoggetinnIkkeOK()
        {
            mockRep.Setup(k => k.DeleteStock(2,1)).ReturnsAsync(false);

            var watchlistController = new WatchlistController(mockRep.Object, mockLog.Object);

            mockSession[_loggetInn] = _loggetInn;
            mockHttpContext.Setup(s => s.Session).Returns(mockSession);
            watchlistController.ControllerContext.HttpContext = mockHttpContext.Object;

            // Act
            var resultat = await watchlistController.DeleteStock(2) as BadRequestObjectResult;

            // Assert 
            Assert.Equal((int)HttpStatusCode.BadRequest, resultat.StatusCode);
            Assert.Equal("Could not delete from watchlist", resultat.Value);
        }

        [Fact]
        public async Task DeletestockLoggedInIkkeOK()
        {
            mockRep.Setup(k => k.DeleteStock(2, 1)).ReturnsAsync(false);

            var watchlistController = new WatchlistController(mockRep.Object, mockLog.Object);

            mockSession[_loggetInn] = _loggetInn+_loggetInn;
            mockHttpContext.Setup(s => s.Session).Returns(mockSession);
            watchlistController.ControllerContext.HttpContext = mockHttpContext.Object;

            // Act
            var resultat = await watchlistController.DeleteStock(2) as BadRequestObjectResult;

            // Assert 
            Assert.Equal((int)HttpStatusCode.BadRequest, resultat.StatusCode);
            Assert.Equal("Could not delete from watchlist", resultat.Value);
        }

        [Fact]
        public async Task UpdateStockIkkeOK()
        {
            mockRep.Setup(k => k.UpdateStock(1, 1, 10)).ReturnsAsync(false);

            var watchlistController = new WatchlistController(mockRep.Object, mockLog.Object);

            mockSession[_loggetInn] = null;
            mockHttpContext.Setup(s => s.Session).Returns(mockSession);
            watchlistController.ControllerContext.HttpContext = mockHttpContext.Object;

            // Act
            var resultat = await watchlistController.UpdateStock(1, 10, 100) as UnauthorizedObjectResult;

            // Assert 
            Assert.Equal((int)HttpStatusCode.Unauthorized, resultat.StatusCode);
            Assert.Equal("User is not logged in", resultat.Value);
        }

        [Fact]
        public async Task DeleteStockIkkeOK()
        {
            mockRep.Setup(k => k.DeleteStock(1, 1)).ReturnsAsync(false);

            var watchlistController = new WatchlistController(mockRep.Object, mockLog.Object);

            mockSession[_loggetInn] = null;
            mockHttpContext.Setup(s => s.Session).Returns(mockSession);
            watchlistController.ControllerContext.HttpContext = mockHttpContext.Object;

            // Act
            var resultat = await watchlistController.DeleteStock(1) as UnauthorizedObjectResult;

            // Assert 
            Assert.Equal((int)HttpStatusCode.Unauthorized, resultat.StatusCode);
            Assert.Equal("User is not logged in", resultat.Value);
        }

        [Fact]
        public async Task AddStockIkkeOK()
        {
            mockRep.Setup(k => k.AddStock("googl", 1, 1, 1)).ReturnsAsync(false);

            var watchlistController = new WatchlistController(mockRep.Object, mockLog.Object);

            mockSession[_loggetInn] = null;
            mockHttpContext.Setup(s => s.Session).Returns(mockSession);
            watchlistController.ControllerContext.HttpContext = mockHttpContext.Object;

            // Act
            var resultat = await watchlistController.AddStock("googl", 1, 1) as UnauthorizedObjectResult;

            // Assert 
            Assert.Equal((int)HttpStatusCode.Unauthorized, resultat.StatusCode);
            Assert.Equal("User is not logged in", resultat.Value);
        }

        [Fact]
        public async Task UpdateStockLoggetinnOK()
        {
            var mock = new Mock<IWatchlistRepository>();
            mock.Setup(k => k.UpdateStock(1, 4, 50)).ReturnsAsync(true);
            var watchlistController = new WatchlistController(mock.Object, mockLog.Object);

            mockSession[_loggetInn] = _loggetInn;
            mockHttpContext.Setup(s => s.Session).Returns(mockSession);
            watchlistController.ControllerContext.HttpContext = mockHttpContext.Object;

            var resualt = await watchlistController.UpdateStock(1, 4, 50) as OkObjectResult;

            Assert.Equal((int)HttpStatusCode.OK, resualt.StatusCode);
            Assert.Equal("User updated to watchlist", resualt.Value);
        }

        [Fact]
        public async Task UpdateStockLoggetinnValFeil()
        {
            var mock = new Mock<IWatchlistRepository>();
            mock.Setup(k => k.UpdateStock(1, 4, 50)).ReturnsAsync(true);
            var watchlistController = new WatchlistController(mock.Object, mockLog.Object);

            mockSession[_loggetInn] = _loggetInn;
            mockHttpContext.Setup(s => s.Session).Returns(mockSession);
            watchlistController.ControllerContext.HttpContext = mockHttpContext.Object;

            var resualt = await watchlistController.UpdateStock(-1000, -400000000, -400000000) as BadRequestObjectResult;

            Assert.Equal((int)HttpStatusCode.BadRequest, resualt.StatusCode);
            Assert.Equal("Fault in InputVal", resualt.Value);
        }

        [Fact]
        public async Task UpdatestockLoggetinnIkkeOK()
        {
            mockRep.Setup(k => k.UpdateStock(4, 3, 50)).ReturnsAsync(false);

            var watchlistController = new WatchlistController(mockRep.Object, mockLog.Object);

            mockSession[_loggetInn] = _loggetInn;
            mockHttpContext.Setup(s => s.Session).Returns(mockSession);
            watchlistController.ControllerContext.HttpContext = mockHttpContext.Object;

            // Act
            var resultat = await watchlistController.UpdateStock(4, 3, 50) as BadRequestObjectResult;

            // Assert 
            Assert.Equal((int)HttpStatusCode.BadRequest, resultat.StatusCode);
            Assert.Equal("Could not update watchlist", resultat.Value);
        }

        [Fact]
        public async Task AddStockWrongInputVal()
        {
            mockRep.Setup(k => k.AddStock("44",1,50,1)).ReturnsAsync(true);

            var watchlistController = new WatchlistController(mockRep.Object, mockLog.Object);

            watchlistController.ModelState.AddModelError("InputValidation", "Fault in InputVal");

            mockSession[_loggetInn] = _loggetInn;
            mockHttpContext.Setup(s => s.Session).Returns(mockSession);
            watchlistController.ControllerContext.HttpContext = mockHttpContext.Object;

            // Act
            var resultat = await watchlistController.AddStock("44", 1, 50) as BadRequestObjectResult;

            // Assert 
            Assert.Equal((int)HttpStatusCode.BadRequest, resultat.StatusCode);
            Assert.Equal("Fault in InputVal", resultat.Value);
        }
    }
}

