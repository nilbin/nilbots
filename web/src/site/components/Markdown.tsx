import { useMemo } from 'react';
import { marked } from 'marked';

/// Renders one of the repo's own markdown documents in the site's visual language.
///
/// The source is imported at BUILD time from a file we author (see DocsPage), never
/// from user input or the network, which is what makes the innerHTML below safe: there
/// is no untrusted path into it. Keeping this component dumb — markdown in, styled HTML
/// out — is deliberate, because its whole reason to exist is that the rules prose must
/// have exactly one source (the site, /llms-full.txt and every scaffolded README all
/// render the same file).
export default function Markdown({ source }: { source: string }) {
  const html = useMemo(
    () => marked.parse(source, { async: false, gfm: true, breaks: false }) as string,
    [source],
  );
  return (
    <div
      className="markdown-body flex flex-col gap-3"
      dangerouslySetInnerHTML={{ __html: html }}
    />
  );
}
