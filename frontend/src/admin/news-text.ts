import type { Block } from './types';

/**
 * The article text is edited as plain writing, one box per language, and stored as the structured
 * blocks the API expects (paragraph, heading, list, quote, picture). The writing rules are the
 * ones people already use when typing:
 *
 *   An empty line starts a new paragraph.
 *   ## Subheading            (### for a smaller one)
 *   - a list item            (1. for a numbered list)
 *   > a quotation            (a last line "> — Name" says who said it)
 *   [[picture:12 | caption]] a picture from the library (the Picture button writes this line)
 *
 * Inside a paragraph, **bold** and [link text](https://…) work as on the website.
 */

const PICTURE = /^\[\[\s*(?:picture|image|صورة)\s*:\s*(\d+)\s*(?:\|\s*(.*?))?\s*\]\]$/iu;
const BULLET = /^\s*[-•*]\s+/;
const NUMBER = /^\s*\d+[.)]\s+/;
const QUOTE = /^\s*>\s?/;
const CITE = /^\s*[—–-]{1,2}\s*/;

export function blocksToText(blocks: Block[], pictureWord = 'picture'): string {
  return blocks
    .map((b) => {
      switch (b.type) {
        case 'paragraph':
          return b.text;
        case 'heading':
          return `${b.level === 3 ? '###' : '##'} ${b.text}`;
        case 'list':
          return b.items.map((item, i) => (b.style === 'number' ? `${i + 1}. ${item}` : `- ${item}`)).join('\n');
        case 'quote': {
          const lines = b.text.split('\n').map((l) => `> ${l}`);
          if (b.cite) lines.push(`> — ${b.cite}`);
          return lines.join('\n');
        }
        case 'image':
          return b.mediaId ? `[[${pictureWord}:${b.mediaId}${b.caption ? ` | ${b.caption}` : ''}]]` : '';
        default:
          return '';
      }
    })
    .filter((part) => part.length > 0)
    .join('\n\n');
}

export function textToBlocks(text: string): Block[] {
  const blocks: Block[] = [];
  const chunks = text.replace(/\r\n?/g, '\n').split(/\n[ \t]*\n/);
  for (const raw of chunks) {
    let lines = raw.split('\n').filter((l, i, all) => !(l.trim() === '' && (i === 0 || i === all.length - 1)));
    while (lines.length) {
      const first = lines[0]!.trim();
      // A subheading or a picture takes its own line; what follows it is the next block.
      const heading = /^(#{2,3})\s+(.+)$/.exec(first);
      if (heading) {
        blocks.push({ type: 'heading', text: heading[2]!.trim(), level: heading[1]!.length === 3 ? 3 : 2 });
        lines = lines.slice(1);
        continue;
      }
      const pic = PICTURE.exec(first);
      if (pic) {
        blocks.push({ type: 'image', mediaId: Number(pic[1]), caption: (pic[2] ?? '').trim() });
        lines = lines.slice(1);
        continue;
      }
      if (BULLET.test(lines[0]!) || NUMBER.test(lines[0]!)) {
        const numbered = NUMBER.test(lines[0]!);
        const marker = numbered ? NUMBER : BULLET;
        const items: string[] = [];
        let i = 0;
        for (; i < lines.length && marker.test(lines[i]!); i++) items.push(lines[i]!.replace(marker, '').trim());
        blocks.push({ type: 'list', style: numbered ? 'number' : 'bullet', items: items.filter(Boolean) });
        lines = lines.slice(i);
        continue;
      }
      if (QUOTE.test(lines[0]!)) {
        const quoted: string[] = [];
        let i = 0;
        for (; i < lines.length && QUOTE.test(lines[i]!); i++) quoted.push(lines[i]!.replace(QUOTE, ''));
        let cite = '';
        const last = quoted.at(-1) ?? '';
        if (quoted.length > 1 && CITE.test(last)) {
          cite = last.replace(CITE, '').trim();
          quoted.pop();
        }
        blocks.push({ type: 'quote', text: quoted.join('\n').trim(), cite });
        lines = lines.slice(i);
        continue;
      }
      // A paragraph runs until a line that starts something else.
      const para: string[] = [];
      let i = 0;
      for (; i < lines.length; i++) {
        const l = lines[i]!;
        if (i > 0 && (/^#{2,3}\s+/.test(l.trim()) || PICTURE.test(l.trim()) || BULLET.test(l) || NUMBER.test(l) || QUOTE.test(l))) break;
        para.push(l);
      }
      const value = para.join('\n').trim();
      if (value) blocks.push({ type: 'paragraph', text: value });
      lines = lines.slice(i);
    }
  }
  return blocks;
}

/** Words in the text, for the counter under the box. */
export function wordCount(text: string): number {
  const words = text
    .replace(/\[\[[^\]]*\]\]/g, ' ')
    .replace(/[#>*\-•]/g, ' ')
    .trim()
    .split(/\s+/)
    .filter(Boolean);
  return words.length;
}

/** The block a server error path such as "bodyAr[3].text" points to, counted from 1. */
export function blockNumber(path: string): number | null {
  const m = /^body(?:Ar|En)\[(\d+)\]/.exec(path);
  return m ? Number(m[1]) + 1 : null;
}
