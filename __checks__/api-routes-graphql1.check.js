/**
 * To learn more about Playwright Test visit:
 * https://www.checklyhq.com/docs/browser-checks/playwright-test/
 * https://playwright.dev/docs/writing-tests
 */

const { test } = require("@playwright/test")

test("block unnecessary requests", async ({ page }) => {
  // Cancel all analytics or font request
  await page.route(/(analytics|fonts)/, (route) => route.abort())
  // Don't load images to make your check run faster
  await page.route("**/*.{png,jpg,jpeg,webp,svg}", (route) => route.abort())

  // Change checklyhq.com to your site's URL,
  // or, even better, define a ENVIRONMENT_URL environment variable
  // to reuse it across your browser checks
  await page.goto(process.env.ENVIRONMENT_URL || "https://www.checklyhq.com/")

  // Take a screenshot of the current page
  await page.screenshot({ path: "./screenshot.png" })
})
