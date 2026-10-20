import { test, expect } from '../fixtures.ts';

// SPA配信は.NETへ直接接続して検証し、ブラウザのシナリオではdev時にViteを経由する。
test.use({ serveFrontend: false });

test('同一.NETサーバーがSPAを配信し、未知APIにはHTMLを返さない', async ({ request, app }) => {
  test.skip(app.mode !== 'hosted', 'SPAの一体配信はhostedモードだけで検証します。');

  // Arrange
  const unknownPaths = ['/api/not-found', '/events/not-found'];

  // Act
  const page = await request.get('/import/confirm', { headers: { Accept: 'text/html' } });
  const unknown = [];
  for (const path of unknownPaths)
    unknown.push(await request.get(path, { headers: { Accept: 'text/html' } }));

  // Assert
  expect(page.status()).toBe(200);
  expect(page.headers()['content-type']).toContain('text/html');
  expect(await page.text()).toContain('<div id="root">');
  for (const response of unknown) {
    expect(response.status()).toBe(404);
    expect(response.headers()['content-type'] ?? '').not.toContain('text/html');
  }
});
