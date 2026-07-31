import { ThemeProvider } from './context/ThemeProvider'
import { ThemeToggle } from './components/ThemeToggle'
import './App.css'

// ThemeProvider wraps the whole app so any component can use useTheme.
function App() {
  return (
    <ThemeProvider>
      <div className="app">
        <header className="app-header">
          <h1 className="app-title">Capstone Audio Upload</h1>
          <ThemeToggle />
        </header>
        <main className="app-main">
          {/* Future upload area (Phase 2). */}
          <div className="upload-placeholder" />
        </main>
      </div>
    </ThemeProvider>
  )
}

export default App
