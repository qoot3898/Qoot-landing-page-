import { describe, expect, it } from 'vitest';
import { looksLikeEmail, looksLikePhone, minutes, openState } from './interactions';
import { whatsappHref } from './render';

const settings = { openDays: [0, 1, 2, 3, 4, 6], opensAt: '08:00', closesAt: '17:00', timeZone: 'Africa/Tripoli' };
const words = { open: 'Open until {to}', today: 'Opens today at {from}', tomorrow: 'Opens tomorrow at {from}', day: 'Opens {day} at {from}' };

describe('Contact us form helpers', () => {
  it('accepts the ways people write a phone number', () => {
    expect(looksLikePhone('+218 91 234 5678')).toBe(true);
    expect(looksLikePhone('091-2345678')).toBe(true);
    expect(looksLikePhone('(091) 234 5678')).toBe(true);
    expect(looksLikePhone('call me')).toBe(false);
    expect(looksLikePhone('12345')).toBe(false);
    expect(looksLikePhone('ops@company.ly')).toBe(false);
  });

  it('checks an email only for its shape', () => {
    expect(looksLikeEmail('ops@company.ly')).toBe(true);
    expect(looksLikeEmail(' name@qoot.com ')).toBe(true);
    expect(looksLikeEmail('a@b')).toBe(false);
    expect(looksLikeEmail('not an email')).toBe(false);
  });

  it('makes a WhatsApp link from the phone number in Settings', () => {
    expect(whatsappHref('+218 123 456 789')).toBe('https://wa.me/218123456789');
    expect(whatsappHref('00218912345678')).toBe('https://wa.me/218912345678');
    expect(whatsappHref('123')).toBeNull();
  });
});

describe('open now', () => {
  it('reads minutes', () => expect(minutes('08:30')).toBe(510));

  it('is open on a working day inside hours (Benghazi time)', () => {
    // Sunday 2026-10-18 10:00 in Libya = 08:00 UTC
    const s = openState(new Date('2026-10-18T08:00:00Z'), settings, words, 'en')!;
    expect(s.isOpen).toBe(true);
    expect(s.message).toBe('Open until 17:00');
    expect(s.day).toBe(0);
  });

  it('opens later today before opening time', () => {
    const s = openState(new Date('2026-10-18T04:00:00Z'), settings, words, 'en')!;
    expect(s.isOpen).toBe(false);
    expect(s.message).toBe('Opens today at 8:00');
  });

  it('skips the closed Friday', () => {
    // Thursday 2026-10-22 18:00 Libya → next open day is Saturday
    const s = openState(new Date('2026-10-22T16:00:00Z'), settings, words, 'en')!;
    expect(s.message).toBe('Opens Saturday at 8:00');
    const t = openState(new Date('2026-10-21T16:00:00Z'), settings, words, 'en')!;
    expect(t.message).toBe('Opens tomorrow at 8:00');
  });

  it('shows nothing when no opening days are set', () => {
    expect(openState(new Date(), { ...settings, openDays: [] }, words, 'en')).toBeNull();
  });
});
