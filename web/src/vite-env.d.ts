/// <reference types="vite/client" />

// Raw text imports: the docs pages compile the repo's own markdown straight into the
// bundle so the site cannot drift from the canonical source.
declare module '*.md?raw' {
  const content: string;
  export default content;
}
