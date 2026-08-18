import type { SheetIssue } from './tacticsDocument';

/** The editor's issue readout; clicking an issue reveals its subject. */
export function IssueSummary({ issues, onReveal }: {
  issues: SheetIssue[];
  onReveal?: (issue: SheetIssue) => void;
}) {
  if (issues.length === 0) return null;
  const errors = issues.filter((issue) => issue.severity === 'error');
  const warnings = issues.filter((issue) => issue.severity === 'warning');
  return (
    <div className="panel-quiet pad" role="alert">
      <div className="lab mb-1">
        {errors.length > 0
          ? `${errors.length} thing${errors.length === 1 ? '' : 's'} to fix before saving`
          : 'Worth a look'}
      </div>
      <ul className="space-y-0.5">
        {[...errors, ...warnings].slice(0, 6).map((issue, index) => (
          <li key={`${issue.path}:${index}`}>
            <button
              type="button"
              onClick={() => onReveal?.(issue)}
              className={`text-left t-micro ${issue.severity === 'error' ? 'text-arena-hot' : 'text-arena-material'} hover:underline`}
            >
              {issue.message}
            </button>
          </li>
        ))}
        {issues.length > 6 && (
          <li className="t-micro text-arena-dim">…and {issues.length - 6} more.</li>
        )}
      </ul>
    </div>
  );
}
