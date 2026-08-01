import { ThemeProvider } from './context/ThemeProvider'
import { ThemeToggle } from './components/ThemeToggle'
import { UploadForm } from './components/UploadForm'
import './App.css'

function App() {
  return (
    <ThemeProvider>
      <div className="app">
        <header className="app-header">
          <h1 className="app-title">Capstone Project</h1>
          <ThemeToggle />
        </header>
        <main className="app-main">
          <section className="upload-section">
            <h2 className="upload-section-title">Upload a recording</h2>
            <UploadForm />
          </section>
        </main>
      </div>
    </ThemeProvider>
  )
}

export default App
