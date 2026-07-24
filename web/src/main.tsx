import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import './index.css';
import App from './App';
import Site from './site/Site';

// One build, two modes: with an embedded replay (or served from a file) this is the
// standalone match viewer the CLI ships; otherwise it is the nilbots website.
const standalone =
  Boolean(window.__BOTARENA_REPLAY__) ||
  window.location.protocol === 'file:' ||
  new URLSearchParams(window.location.search).has('standalone');

createRoot(document.getElementById('root')!).render(
  <StrictMode>{standalone ? <App /> : <Site />}</StrictMode>,
);
