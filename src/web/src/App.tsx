import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import { CssBaseline, AppBar, Toolbar, Typography, Container, Box, Button } from '@mui/material';
import { WorkQueueList } from './pages/WorkQueueList';
import { WorkQueueDetailPage } from './pages/WorkQueueDetailPage';
import { AdminPage } from './pages/AdminPage';

const theme = createTheme({
  palette: {
    primary: {
      main: '#1976d2',
    },
    secondary: {
      main: '#dc004e',
    },
  },
});

function App() {
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <BrowserRouter>
        <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100vh' }}>
          <AppBar position="static">
            <Toolbar>
              <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
                Epic Billing Analytics & Triage
              </Typography>
              <Button color="inherit" href="/workqueue">
                Work Queue
              </Button>
              <Button color="inherit" href="/admin">
                Admin
              </Button>
            </Toolbar>
          </AppBar>
          
          <Container maxWidth="xl" sx={{ mt: 0, mb: 4, flexGrow: 1 }}>
            <Routes>
              <Route path="/" element={<Navigate to="/workqueue" replace />} />
              <Route path="/workqueue" element={<WorkQueueList />} />
              <Route path="/workqueue/:id" element={<WorkQueueDetailPage />} />
              <Route path="/admin" element={<AdminPage />} />
            </Routes>
          </Container>

          <Box component="footer" sx={{ py: 3, px: 2, mt: 'auto', backgroundColor: '#f5f5f5' }}>
            <Container maxWidth="xl">
              <Typography variant="body2" color="text.secondary" align="center">
                Epic Billing Analytics & Triage - Demo Application
              </Typography>
            </Container>
          </Box>
        </Box>
      </BrowserRouter>
    </ThemeProvider>
  );
}

export default App;
