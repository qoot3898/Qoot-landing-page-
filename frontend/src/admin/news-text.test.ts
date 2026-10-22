import { describe, expect, it } from 'vitest';
import { blockNumber, blocksToText, textToBlocks, wordCount } from './news-text';
import type { Block } from './types';

describe('article text ⇄ blocks', () => {
  it('reads paragraphs, subheadings, lists, quotes and pictures', () => {
    const text = [
      'وقّعت قوت عقدًا لمدة عام.\nيبدأ التوريد في نوفمبر.',
      '## ما يشمله العقد',
      '- ثلاث وجبات يوميًا\n- مياه شرب معبأة',
      '1. first\n2. second',
      '> We are proud of this.\n> — Ahmed',
      '[[picture:12 | The first truck]]',
      'Last **bold** words with a [link](https://www.qoot.com/).',
    ].join('\n\n');
    expect(textToBlocks(text)).toEqual<Block[]>([
      { type: 'paragraph', text: 'وقّعت قوت عقدًا لمدة عام.\nيبدأ التوريد في نوفمبر.' },
      { type: 'heading', text: 'ما يشمله العقد', level: 2 },
      { type: 'list', style: 'bullet', items: ['ثلاث وجبات يوميًا', 'مياه شرب معبأة'] },
      { type: 'list', style: 'number', items: ['first', 'second'] },
      { type: 'quote', text: 'We are proud of this.', cite: 'Ahmed' },
      { type: 'image', mediaId: 12, caption: 'The first truck' },
      { type: 'paragraph', text: 'Last **bold** words with a [link](https://www.qoot.com/).' },
    ]);
  });

  it('lets a subheading or a list follow a line without an empty line between', () => {
    expect(textToBlocks('### Small heading\nThe text under it.\n- one\n- two')).toEqual<Block[]>([
      { type: 'heading', text: 'Small heading', level: 3 },
      { type: 'paragraph', text: 'The text under it.' },
      { type: 'list', style: 'bullet', items: ['one', 'two'] },
    ]);
  });

  it('accepts the Arabic word for a picture and ignores extra empty lines', () => {
    expect(textToBlocks('\n\n[[صورة:7]]\n\n\n\nنص.\n\n')).toEqual<Block[]>([
      { type: 'image', mediaId: 7, caption: '' },
      { type: 'paragraph', text: 'نص.' },
    ]);
  });

  it('writes blocks back as the same text (a round trip changes nothing)', () => {
    const blocks: Block[] = [
      { type: 'paragraph', text: 'One.\nTwo.' },
      { type: 'heading', text: 'A heading', level: 3 },
      { type: 'list', style: 'number', items: ['a', 'b'] },
      { type: 'quote', text: 'Said.', cite: 'Someone' },
      { type: 'image', mediaId: 3, caption: '' },
    ];
    const text = blocksToText(blocks, 'صورة');
    expect(text).toContain('[[صورة:3]]');
    expect(textToBlocks(text)).toEqual(blocks);
  });

  it('counts words and finds the block an API error points to', () => {
    expect(wordCount('## Title\n\nTwo words [[picture:1]]')).toBe(3);
    expect(blockNumber('bodyAr[3].text')).toBe(4);
    expect(blockNumber('titleAr')).toBeNull();
  });
});
