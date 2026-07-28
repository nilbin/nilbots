import { useMemo } from 'react';
import { marked, Renderer } from 'marked';

/// Renders one of the repo's own markdown documents in the site's visual language.
///
/// The source is imported at BUILD time from a file we author (see DocsPage), never
/// from user input or the network, which is what makes the innerHTML below safe: there
/// is no untrusted path into it. Keeping this component dumb — markdown in, styled HTML
/// out — is deliberate, because its whole reason to exist is that the rules prose must
/// have exactly one source (the site, /llms-full.txt and every scaffolded README all
/// render the same file).
export default function Markdown({
  source,
  headingOffset = 0,
}: {
  source: string;
  headingOffset?: number;
}) {
  const html = useMemo(
    () =>
      marked.parse(source, {
        async: false,
        gfm: true,
        breaks: false,
        renderer: headingRenderer(headingOffset),
      }) as string,
    [headingOffset, source],
  );
  return (
    <div
      className="markdown-body flex flex-col gap-3"
      dangerouslySetInnerHTML={{ __html: html }}
    />
  );
}

/**
 * Imported documents retain their internal outline while fitting beneath the
 * heading that introduces them on the current page. A renderer-level offset
 * handles ATX and setext headings alike and preserves Marked's inline parsing.
 */
function headingRenderer(offset: number): Renderer {
  const renderer = new Renderer();
  const normalizedOffset = Number.isFinite(offset) ? Math.trunc(offset) : 0;
  renderer.heading = function ({ tokens, depth }) {
    const level = Math.max(1, Math.min(6, depth + normalizedOffset));
    return `<h${level}>${this.parser.parseInline(tokens)}</h${level}>\n`;
  };
  return renderer;
}
