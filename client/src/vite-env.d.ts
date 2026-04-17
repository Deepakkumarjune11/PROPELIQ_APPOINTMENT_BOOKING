/// <reference types="vite/client" />

// Extend Vite's ImportMetaEnv with project-specific variables.
// All variables MUST be prefixed with VITE_ to be exposed to client-side code.
// See .env.example for the full list of required variables.
interface ImportMetaEnv {
  /** Base URL for the PropelIQ .NET 8 backend API */
  readonly VITE_API_BASE_URL: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
