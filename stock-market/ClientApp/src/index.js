import 'bootstrap/dist/css/bootstrap.css';
import React from 'react';
import ReactDOM from 'react-dom';
import registerServiceWorker from './registerServiceWorker';
import { BrowserRouter, Routes } from 'react-router-dom';

import App from './App';

import { createTheme, ThemeProvider } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';


const baseUrl = document.getElementsByTagName('base')[0].getAttribute('href');
const rootElement = document.getElementById('root');


const customTheme = createTheme({
    palette: {
        primary: {
            main: '#252638',
            contrastText: '#FEFEFE'
        },
        background: {
            default: '#27262B',
        },
        error: {
            main: '#f5374a'
        },
        success: {
            main: '#4fa579'
        },
        type: 'dark',
    },
    typography: {
        fontFamily: [
            'Public Sans',
        ].join(','),
    }
});

    ReactDOM.render(
        <ThemeProvider theme={customTheme}>
            <CssBaseline />
            <BrowserRouter>
                    <App />
            </BrowserRouter>
        </ThemeProvider>,
        rootElement)
registerServiceWorker();

