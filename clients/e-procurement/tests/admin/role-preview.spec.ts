import { expect, test } from "@playwright/test";
import { mockJsonResponse } from "../helpers/api-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { BUYER_PERMS, installEprocShellMocks, paged } from "../helpers/shell-mocks";

const BUYER_ROLE_ID = "11111111-1111-1111-1111-111111111111";
const ROLES_VIEW = "Permissions.Roles.View";
const REQ_VIEW = "Permissions.Sourcing.Requisitions.View";

const ADMIN_PERMS = [...BUYER_PERMS, ROLES_VIEW] as const;

function navButton(page: import("@playwright/test").Page, name: string) {
  return page.getByLabel("Main navigation").getByRole("link", { name, exact: true });
}

test.describe("Act as role preview", () => {
  test("picker is hidden without Roles.View", async ({ page }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions: [...BUYER_PERMS] });
    await installEprocShellMocks(page);
    await page.goto("/dashboard");
    await expect(page.getByLabel("Act as role")).toHaveCount(0);
  });

  test("picker is hidden for vendor logins even with Roles.View", async ({ page }) => {
    await seedAuthedSession(page, {
      ...TEST_USER,
      sub: "u-vendor-1",
      vendorId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      permissions: [...ADMIN_PERMS],
    });
    await installEprocShellMocks(page, ADMIN_PERMS);
    await mockJsonResponse(page, "**/api/v1/sourcing/rfqs**", []);
    await mockJsonResponse(page, "**/api/v1/sourcing/bids**", []);
    await page.goto("/dashboard");
    await expect(page.getByLabel("Act as role")).toHaveCount(0);
  });

  test("switching a role updates nav and Stop preview restores it", async ({ page }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions: [...ADMIN_PERMS] });
    await installEprocShellMocks(page, ADMIN_PERMS);
    await mockJsonResponse(
      page,
      "**/api/v1/identity/roles**",
      paged([
        {
          id: BUYER_ROLE_ID,
          name: "Buyer",
          description: "Buyer",
          createdOnUtc: "2026-01-01T00:00:00Z",
        },
      ]),
    );
    await mockJsonResponse(page, `**/api/v1/identity/${BUYER_ROLE_ID}/permissions**`, {
      id: BUYER_ROLE_ID,
      name: "Buyer",
      permissions: [REQ_VIEW],
    });

    await page.goto("/dashboard");
    const actAs = page.getByLabel("Act as role");
    await expect(actAs).toBeVisible();
    await expect(navButton(page, "Vendor Master")).toBeVisible();
    await expect(navButton(page, "Requisitions")).toBeVisible();

    await actAs.selectOption(BUYER_ROLE_ID);
    await expect(page.getByRole("status").filter({ hasText: "Previewing role" })).toBeVisible();
    await expect(navButton(page, "Vendor Master")).toHaveCount(0);
    await expect(navButton(page, "Requisitions")).toBeVisible();

    await page.getByRole("button", { name: "Stop preview" }).click();
    await expect(page.getByRole("status").filter({ hasText: "Previewing role" })).toHaveCount(0);
    await expect(navButton(page, "Vendor Master")).toBeVisible();
    await expect(actAs).toHaveValue("");
  });
});
