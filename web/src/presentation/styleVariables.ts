import type { CSSProperties } from 'react';

type CSSVariableName = `--${string}`;
type CSSVariableValue = string | number | undefined;

/**
 * Keep JSX styling declarative: CSS owns visual rules, while runtime geometry and
 * server-provided colours enter those rules only as typed custom-property values.
 */
export function styleVariables(
  values: Record<CSSVariableName, CSSVariableValue>,
): CSSProperties {
  return values as CSSProperties;
}
