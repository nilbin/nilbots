/** A grid of title + blurb selection cards, the wizard's larger either/or. */
export function ChoiceCards<T extends string>({ options, value, columns = 1, onChange }: {
  options: { id: T; label: string; blurb: string }[];
  value: T | null;
  columns?: 1 | 2;
  onChange: (id: T) => void;
}) {
  return (
    <div className={`grid gap-1.5 ${columns === 2 ? 'grid-cols-2' : 'grid-cols-1'}`}>
      {options.map((option) => (
        <button
          key={option.id}
          type="button"
          onClick={() => onChange(option.id)}
          className="choice-card text-left"
          data-selected={option.id === value}
        >
          <span className="block text-[13px] font-semibold">{option.label}</span>
          <span className="mt-0.5 block t-micro text-arena-dim">{option.blurb}</span>
        </button>
      ))}
    </div>
  );
}
