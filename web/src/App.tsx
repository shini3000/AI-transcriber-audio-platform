import { useState } from 'react'
import { ThemeProvider } from './context/ThemeProvider'
import { ThemeToggle } from './components/ThemeToggle'
import { UploadForm } from './components/UploadForm'
import { AudioList } from './components/AudioList'
import './App.css'

type ViewType = 'upload' | 'history'

function App() {
  const [view, setView] = useState<ViewType>('upload')
  const [refreshTrigger, setRefreshTrigger] = useState(0)

  const handleUploadSuccess = () => {
    setRefreshTrigger((prev) => prev + 1)
  }

  return (
    <ThemeProvider>
      <div className="app">
        <header className="app-header">
          <div className="app-header-left">
            <h1 className="app-title">Capstone Project</h1>
            <nav className="app-nav" aria-label="Main Navigation">
              <button
                type="button"
                className={`app-nav-btn ${view === 'upload' ? 'active' : ''}`}
                onClick={() => setView('upload')}
                aria-current={view === 'upload' ? 'page' : undefined}
              >
                New Recording
              </button>
              <button
                type="button"
                className={`app-nav-btn ${view === 'history' ? 'active' : ''}`}
                onClick={() => setView('history')}
                aria-current={view === 'history' ? 'page' : undefined}
              >
                History
              </button>
            </nav>
          </div>
          <ThemeToggle />
        </header>
        <main className="app-main">
          {view === 'upload' ? (
            <section className="upload-section">
              <h2 className="upload-section-title">Upload a recording</h2>
              <UploadForm onUploadSuccess={handleUploadSuccess} />
            </section>
          ) : (
            <section className="history-section">
              <AudioList refreshTrigger={refreshTrigger} />
            </section>
          )}
        </main>
      </div>
    </ThemeProvider>
  )
}

export default App
