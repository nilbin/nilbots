export interface CrestPresentation {
  shape: string;
  pattern: string;
  mark: string;
  primary: string;
  secondary: string;
  detail: string;
}

export default function EntrantCrest({ crest, size = 40 }: { crest: CrestPresentation; size?: number }) {
  const path = crest.shape === 'roundel' ? 'M32 4a28 28 0 1 1 0 56 28 28 0 0 1 0-56Z'
    : crest.shape === 'diamond' ? 'M32 3 61 32 32 61 3 32Z'
      : crest.shape === 'hex' ? 'M17 5h30l15 27-15 27H17L2 32Z'
        : crest.shape === 'notched' ? 'M6 6h52v20l-8 6 8 6v20H6V38l8-6-8-6Z'
          : 'M7 6h50v25c0 16-10 25-25 30C17 56 7 47 7 31Z';
  const clipId = `crest-${crest.mark}-${crest.shape}-${crest.primary.replace('#', '')}`;
  return <svg viewBox="0 0 64 64" width={size} height={size} role="img" aria-label={`${crest.mark} crest`}
    className="shrink-0 drop-shadow-[0_0_8px_rgba(255,255,255,.12)]">
    <defs><clipPath id={clipId}><path d={path} /></clipPath></defs>
    <g clipPath={`url(#${clipId})`}>
      <rect width="64" height="64" fill={crest.secondary} />
      {crest.pattern === 'split' && <path d="M32 0h32v64H32Z" fill={crest.primary} opacity=".9" />}
      {crest.pattern === 'band' && <path d="M0 22h64v20H0Z" fill={crest.primary} />}
      {crest.pattern === 'chevron' && <path d="m0 13 32 23 32-23v16L32 53 0 29Z" fill={crest.primary} />}
      {crest.pattern === 'quartered' && <><path d="M0 0h32v32H0Z" fill={crest.primary}/><path d="M32 32h32v32H32Z" fill={crest.primary}/></>}
      {crest.pattern === 'core' && <circle cx="32" cy="32" r="19" fill={crest.primary} />}
    </g>
    <path d={path} fill="none" stroke={crest.detail} strokeWidth="2.5" />
    <text x="32" y="40" textAnchor="middle" fontSize="23" fontWeight="900" fill={crest.detail}
      fontFamily="ui-monospace, monospace">{crest.mark.slice(0, 1).toUpperCase()}</text>
  </svg>;
}
