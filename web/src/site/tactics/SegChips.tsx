/** A single-choice row of small chips, accented per group where one applies. */
export function SegChips<T extends string>({ options, value, accent, onChange }: {
  options: { id: T; label: string }[];
  value: T;
  accent?: string;
  onChange: (id: T) => void;
}) {
  return (
    <div className="flex flex-wrap gap-1">
      {options.map((option) => {
        const on = option.id === value;
        return (
          <button
            key={option.id}
            type="button"
            onClick={() => onChange(option.id)}
            className={`min-h-8 rounded-sm border px-2.5 t-micro transition-colors ${on
              ? 'border-transparent font-semibold text-[#06121a]'
              : 'border-arena-edge text-arena-dim hover:text-arena-text'}`}
            style={on ? { background: accent ?? 'var(--color-arena-text)' } : undefined}
          >
            {option.label}
          </button>
        );
      })}
    </div>
  );
}
