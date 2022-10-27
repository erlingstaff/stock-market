﻿import React from 'react';

import { TopBar } from '../components/TopBar.jsx';
import StocksData from '../fetchingData/StocksData';
import { drawerWidth } from '../components/Nav';
 

import Container from '@mui/material/Container';
import { useTheme } from '@mui/material/styles';



export default function Dashboard() {

    const customTheme = useTheme();

    return (
        <>
            <TopBar title='All Stocks' />

            <Container sx={{
                ml: drawerWidth+'px',
                width: 'auto',
            }}>
                <StocksData />
            </Container>
        </>
    );
}



