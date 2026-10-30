// Makes fsemi/splashbg.png and fsemi/logo2.png from the logo SVGs here.
// usage: node make_brand_assets.js fsemi/branding fsemi   (needs Playwright with Chromium, and
// Inter18pt-Light.woff2 / Inter18pt-Regular.woff2 from fpt-semiconductor.com in fsemi/branding)
const { chromium } = require('playwright');
const fs = require('fs');
(async () => {
  const [dir, out] = process.argv.slice(2);
  const dark = fs.readFileSync(`${dir}/logo-on-dark.svg`, 'utf8').replace(/width="247" height="57"/, 'width="100%" height="100%"');
  const font = w => 'data:font/woff2;base64,' + fs.readFileSync(`${dir}/Inter18pt-${w}.woff2`).toString('base64');
  const b = await chromium.launch();
  const p = await b.newPage({ viewport: { width: 600, height: 375 } });
  // splash 600x375: the window shows the middle 584x336; version text sits at about x 411-566, y 127-152
  await p.setContent(`<html><body style="margin:0;width:600px;height:375px;overflow:hidden;
    background:radial-gradient(ellipse 420px 260px at 470px 330px, #1d3a8f 0%, #0a1660 40%, #010837 80%);font-family:Inter,sans-serif"><style>@font-face{font-family:Inter;font-weight:300;src:url(${font('Light')})}@font-face{font-family:Inter;font-weight:400;src:url(${font('Regular')})}</style>
    <div style="position:absolute;left:44px;top:112px;width:300px;height:69px">${dark}</div>
    <div style="position:absolute;left:46px;top:196px;color:#fff;font-size:34px;font-weight:300;letter-spacing:0.5px">Mission Planner</div>
    <div style="position:absolute;left:47px;top:246px;width:56px;height:3px;background:linear-gradient(90deg,#2E65A5 0 33%,#DB773A 33% 66%,#50A657 66%)"></div>
    <div style="position:absolute;left:47px;top:300px;color:#9DB3F9;font-size:13px">fpt-semiconductor.com</div>
  </body></html>`);
  await p.evaluate(() => document.fonts.ready); await p.screenshot({ path: `${out}/splashbg.png` });
  // menu bar logo 200x31, white wordmark for the dark bar
  await p.setViewportSize({ width: 200, height: 31 });
  await p.setContent(`<html><body style="margin:0;background:transparent"><div style="position:absolute;left:33px;top:0;width:134px;height:31px">${dark}</div></body></html>`);
  await p.screenshot({ path: `${out}/logo2.png`, omitBackground: true });
  await b.close();
})();
