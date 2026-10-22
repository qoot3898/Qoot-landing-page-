import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api, ApiError, resetAntiforgery } from './api';

const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

describe('api client', () => {
  let fetchMock: ReturnType<typeof vi.fn>;
  beforeEach(() => {
    resetAntiforgery();
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
  });
  afterEach(() => vi.unstubAllGlobals());

  it('GETs with cookies and the page language, without an antiforgery token', async () => {
    fetchMock.mockResolvedValueOnce(json(200, { ok: 1 }));
    await expect(api('/api/public/site', { lang: 'en' })).resolves.toEqual({ ok: 1 });
    const [url, init] = fetchMock.mock.calls[0]!;
    expect(url).toBe('/api/public/site');
    expect(init.credentials).toBe('include');
    expect(init.headers['Accept-Language']).toBe('en');
    expect(init.headers['X-XSRF-TOKEN']).toBeUndefined();
  });

  it('sends the antiforgery token on writes and renews it once when it expired', async () => {
    fetchMock
      .mockResolvedValueOnce(json(200, { token: 't1', headerName: 'X-XSRF-TOKEN' }))
      .mockResolvedValueOnce(json(400, { code: 'antiforgery', title: 'Expired' }))
      .mockResolvedValueOnce(json(200, { token: 't2', headerName: 'X-XSRF-TOKEN' }))
      .mockResolvedValueOnce(json(201, { id: 5 }));
    await expect(api('/api/admin/news', { method: 'POST', body: { a: 1 } })).resolves.toEqual({ id: 5 });
    expect(fetchMock.mock.calls[1]![1].headers['X-XSRF-TOKEN']).toBe('t1');
    expect(fetchMock.mock.calls[3]![1].headers['X-XSRF-TOKEN']).toBe('t2');
    expect(fetchMock.mock.calls[3]![1].body).toBe('{"a":1}');
  });

  it('turns problem details into ApiError with field errors', async () => {
    fetchMock.mockResolvedValueOnce(json(200, { token: 't' })).mockResolvedValueOnce(
      json(400, { code: 'validation', title: 'Check the fields', errors: { 'title.ar': ['Required'] } }),
    );
    const error = await api('/api/admin/news', { method: 'POST', body: {} }).catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(400);
    expect((error as ApiError).code).toBe('validation');
    expect((error as ApiError).errors['title.ar']).toEqual(['Required']);
  });

  it('reports network failures with status 0 and a localized message', async () => {
    fetchMock.mockRejectedValueOnce(new TypeError('Failed to fetch'));
    const error = (await api('/api/public/site', { lang: 'ar' }).catch((e: unknown) => e)) as ApiError;
    expect(error.isNetwork).toBe(true);
    expect(error.message).toMatch(/[؀-ۿ]/);
  });

  it('treats 204 and 304 as empty', async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 304 }));
    await expect(api('/api/public/site')).resolves.toBeUndefined();
  });
});
