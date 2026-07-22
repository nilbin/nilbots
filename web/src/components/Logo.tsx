export default function Logo({ size = 22 }: { size?: number }) {
  return (
    <span className="flex items-center gap-2">
      <svg width={size} height={size} viewBox="0 0 32 32" aria-hidden>
        <rect width="32" height="32" rx="7" fill="#0a0e14" stroke="#1d2836" />
        <circle cx="16" cy="16" r="9" fill="none" stroke="#38bdf8" strokeWidth="2.5" />
        <path d="M16 7v-4M25 16h4" stroke="#38bdf8" strokeWidth="2.5" strokeLinecap="round" />
        <circle cx="16" cy="16" r="3" fill="#e2f3ff" />
      </svg>
      <span className="font-black tracking-[0.2em] uppercase">
        Bot<span className="text-arena-accent">Arena</span>
      </span>
    </span>
  );
}
