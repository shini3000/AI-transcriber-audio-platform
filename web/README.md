# Capstone Audio Upload — Web

Frontend (React + TypeScript + Vite) for the capstone audio upload project.

Consumes the .NET 8 API (`POST /api/audio/upload`, multipart, field `file`, max 50MB) in later phases.

## Stack

- React 19
- TypeScript (strict)
- Vite 8
- pnpm (do not use npm)

## Commands

```bash
pnpm install
pnpm dev
pnpm build
```

## Structure

```
src/
├── components/   UI components
├── context/      React context providers
├── hooks/        Custom hooks
├── services/     API clients and integrations
├── types/        Shared TypeScript types
└── styles/       CSS variables and base styles
```
