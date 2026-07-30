import { NavLink, Outlet } from 'react-router-dom'
import './App.css'

export default function Layout() {
  return (
    <main className="shell">
      <header className="header">
        <h1>Job Assistant</h1>
        <p className="subtitle">Development connectivity • SaaS foundation</p>
        <nav className="nav">
          <NavLink to="/" end>
            Stack status
          </NavLink>
          <NavLink to="/applications">Applications</NavLink>
          <NavLink to="/agent">AI agent</NavLink>
        </nav>
      </header>
      <Outlet />
    </main>
  )
}
