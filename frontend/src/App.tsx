import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import Layout from './Layout'
import AgentPage from './pages/AgentPage'
import ApplicationsPage from './pages/ApplicationsPage'
import StackStatusPage from './pages/StackStatusPage'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<StackStatusPage />} />
          <Route path="applications" element={<ApplicationsPage />} />
          <Route path="agent" element={<AgentPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}
