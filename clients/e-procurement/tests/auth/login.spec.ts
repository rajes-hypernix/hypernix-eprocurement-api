import { expect, test } from "@playwright/test";
import { mockJsonResponse, mockProblemDetails } from "../helpers/api-mocks";
import { makeAccessToken, TEST_USER } from "../helpers/auth-seed";
import { BUYER_PERMS, installEprocShellMocks, paged } from "../helpers/shell-mocks";

// Login posts to /api/v1/identity/token/issue with tenant + X-FSH-App: eprocurement.
const TOKEN_RESPONSE = {
  accessToken: makeAccessToken({ ...TEST_USER, permissions: [...BUYER_PERMS] }),
  refreshToken: "fake-refresh-token",
  accessTokenExpiresAt: "2099-01-01T00:00:00Z",
  refreshTokenExpiresAt: "2099-01-08T00:00:00Z",
};

test.describe("e-procurement login", () => {
  test("renders brand + sign-in form", async ({ page }) => {
    await page.goto("/login");

    await expect(page.getByText("Hypernix eProcure").first()).toBeVisible();
    await expect(page.getByText("Sign in to continue")).toBeVisible();
    await expect(page.getByLabel("Tenant")).toBeVisible();
    await expect(page.getByLabel("Email")).toBeVisible();
    await expect(page.getByLabel("Password", { exact: true })).toBeVisible();
    await expect(page.getByRole("button", { name: "Sign in", exact: true })).toBeVisible();
  });

  test("posts to token/issue with eprocurement app + tenant headers", async ({ page }) => {
    await mockJsonResponse(page, "**/api/v1/identity/token/issue", TOKEN_RESPONSE);
    await installEprocShellMocks(page);
    await mockJsonResponse(page, "**/api/v1/suppliers/vendors**", paged([]));
    await mockJsonResponse(page, "**/api/v1/suppliers/swec**", []);

    await page.goto("/login");
    await page.getByLabel("Tenant").fill("root");
    await page.getByLabel("Email").fill("buyer@root.com");
    await page.getByLabel("Password", { exact: true }).fill("Password123!");

    const reqPromise = page.waitForRequest(
      (r) => r.url().includes("/api/v1/identity/token/issue") && r.method() === "POST",
      { timeout: 5_000 },
    );
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    const req = await reqPromise;

    expect(req.headers()["x-fsh-app"]).toBe("eprocurement");
    expect(req.headers().tenant).toBe("root");
    const body = JSON.parse(req.postData() ?? "{}") as { email: string; password: string };
    expect(body.email).toBe("buyer@root.com");
    expect(body.password).toBe("Password123!");

    await expect(page).toHaveURL(/\/dashboard$/);
    await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
  });

  test("surfaces a 401 and stays on /login", async ({ page }) => {
    await mockProblemDetails(page, "**/api/v1/identity/token/issue", 401, {
      title: "Unauthorized",
      detail: "Invalid credentials.",
    });

    await page.goto("/login");
    await page.getByLabel("Tenant").fill("root");
    await page.getByLabel("Email").fill("buyer@root.com");
    await page.getByLabel("Password", { exact: true }).fill("wrong");
    await page.getByRole("button", { name: "Sign in", exact: true }).click();

    await expect(page.getByRole("alert")).toContainText("Invalid credentials.");
    await expect(page).toHaveURL(/\/login$/);
  });
});
