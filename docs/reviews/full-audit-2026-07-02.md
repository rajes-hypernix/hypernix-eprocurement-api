# eProcure — Full System Audit (2026-07-02)

Whole-system code review + full UI feature test. Audit only — no application code modified.
Measured against: CLAUDE.md, docs/BUSINESS-RULES.md, docs/DATA-MODEL.md, docs/CONVENTIONS.md,
docs/ARCHITECTURE.md, docs/pr-module/* and docs/vendor-onboarding/* (specs, DATA-MODEL-ANALYTICS,
ENGINEERING-STANDARDS, EDGE-CASES).

Structure: **Step 0** Feature Map (this section) · **Step 1** six-lens review · **Step 2** per-feature
UI test results (Playwright) · **Step 3** gap report.

---

# STEP 0 — FEATURE MAP (built from source, 2026-07-02)

Numbering: **A#** API endpoints · **U#** frontend screens/elements · **D#** domain entities/enums ·
**M#** migrations · **S#** cross-cutting services · **X#** flags (orphans/mismatches).
All later findings reference these numbers.

## A. API endpoints (106 total, 16 controller classes)

> Auth column: what is *declared on the endpoint*. There is **no fallback authorization policy**
> (Program.cs:56 defines named role policies only) and **no `[Authorize]` attribute on any business
> controller** — so "none" means the endpoint is publicly callable. See X1.

### AuthController — `api/auth` (AuthController.cs)
| # | Endpoint | Auth | Req → Res |
|---|---|---|---|
| A1 | GET /api/auth/dev-users | none | — → DevUserDto[] |
| A2 | GET /api/auth/personas | none | — → PersonaDto[] |
| A3 | POST /api/auth/dev-login | none | DevLoginRequest → LoginResponse (JWT) |

### AwardsController — `api` (AwardsController.cs)
| A4 | GET /api/rfqs/{rfqId}/award-eligibility | none | — → AwardEligibilityDto |
| A5 | GET /api/rfqs/{rfqId}/award | none | — → AwardDto |
| A6 | POST /api/rfqs/{rfqId}/award | none | SubmitAwardRequest → AwardDto |
| A7 | POST /api/awards/{awardId}/approve | none | — → AwardDto (DoA gate) |
| A8 | GET /api/awards | none | — → AwardDto[] |

### MyInvitations + BidsController (BidsController.cs)
| A9 | GET /api/my/rfqs | none | — → InvitationDto[] (vendor-scoped) |
| A10 | GET /api/rfqs/{rfqId}/my-bid | none | — → BidDto |
| A11 | PUT /api/rfqs/{rfqId}/my-bid | none | SaveBidRequest → BidDto (draft) |
| A12 | POST /api/rfqs/{rfqId}/my-bid/submit | none | SaveBidRequest → BidDto |

### ClarificationsController — `api/clarifications`
| A13 | GET /api/clarifications | none | — → ClarificationThreadDto[] |
| A14 | GET /api/clarifications/thread?scope&vendorId | none | → ClarificationThreadDetail |
| A15 | POST /api/clarifications | none | SendClarificationRequest → ClarificationThreadDetail |

### CustomListsController — `api/custom-lists`
| A16 | GET /api/custom-lists | none | — → CustomListDto[] |
| A17 | GET /api/custom-lists/{code} | none | — → CustomListDto |
| A18 | POST /api/custom-lists | none | CreateCustomListRequest → CustomListDto |
| A19 | POST /api/custom-lists/{code}/values | none | AddCustomListValueRequest → CustomListValueDto |
| A20 | PUT /api/custom-lists/values/{valueId} | none | UpdateCustomListValueRequest → CustomListValueDto |
| A21 | DELETE /api/custom-lists/values/{valueId} | none | — → 204 |

### DashboardController
| A22 | GET /api/dashboard | none | — → DashboardDto (role-adaptive) |

### DeliveriesController — `api`
| A23 | GET /api/asns | none | — → AsnListDto[] |
| A24 | GET /api/asns/{asnId} | none | — → AsnDetailDto |
| A25 | GET /api/asns/{asnId}/grn | none | — → GrnDetailDto |
| A26 | POST /api/asns/{asnId}/receive | none | ReceiveRequest → GrnDetailDto |
| A27 | GET /api/pos/{poId}/ship-plan | none | — → ShipPlanDto |
| A28 | POST /api/pos/{poId}/asns | none | CreateAsnRequest → AsnDetailDto |

### EvaluationController — `api/rfqs/{rfqId}`
| A29 | GET /api/rfqs/{rfqId}/opening | none | — → BidOpeningDto |
| A30 | POST /api/rfqs/{rfqId}/open-technical | none | — → BidOpeningDto |
| A31 | POST /api/rfqs/{rfqId}/open-commercial | none | — → BidOpeningDto (gated on TechFinalized) |
| A32 | GET /api/rfqs/{rfqId}/technical-eval | none | — → TechnicalEvalDto |
| A33 | POST /api/rfqs/{rfqId}/scores | none | SetScoreRequest → TechnicalEvalDto |
| A34 | POST /api/rfqs/{rfqId}/finalize-technical | none | — → TechnicalEvalDto |

### FilesController — `api/files`
| A35 | POST /api/files | none | IFormFile (20 MB cap) → StoredFileInfo |
| A36 | GET /api/files/{id} | none | — → file bytes (inline) |

### HealthController
| A37 | GET /api/health | [AllowAnonymous] | — → HealthResponse |

### InvoicesController — `api`
| A38 | GET /api/invoices | none | — → InvoiceListDto[] |
| A39 | GET /api/invoices/{id} | none | — → InvoiceDetailDto |
| A40 | GET /api/pos/{poId}/billable | none | — → InvoiceBillablePlan |
| A41 | POST /api/pos/{poId}/invoices | none | SubmitInvoiceRequest → InvoiceDetailDto |
| A42 | POST /api/invoices/{id}/approve | none | — → InvoiceDetailDto |
| A43 | POST /api/invoices/{id}/resolve | none | — → InvoiceDetailDto (exception → approved) |

### OnboardingController — `api/onboarding`
| A44 | GET /api/onboarding/templates | none | — → OnboardingTemplateDto[] |
| A45 | GET /api/onboarding/invitations | none | — → OnboardingInvitationDto[] |
| A46 | POST /api/onboarding/invitations | none | SendOnboardingInvitationRequest → OnboardingInvitationDto (+magic link) |
| A47 | POST /api/onboarding/invitations/{id}/resend | none | — → OnboardingInvitationDto |
| A48 | POST /api/onboarding/invitations/{id}/revoke | none | — → 204 |
| A49 | POST /api/onboarding/resolve | [AllowAnonymous] | ResolveOnboardingLinkRequest → OnboardingApplicationDto |
| A50 | GET /api/onboarding/draft?token | [AllowAnonymous] | — → OnboardingDraftDto |
| A51 | PUT /api/onboarding/draft | [AllowAnonymous] | SaveOnboardingDraftRequest → OnboardingDraftDto |
| A52 | POST /api/onboarding/draft/submit | [AllowAnonymous] | ResolveOnboardingLinkRequest → OnboardingDraftDto |
| A53 | POST /api/onboarding/draft/documents | [AllowAnonymous] | IFormFile (20 MB) → OnboardingDocumentDto |
| A54 | DELETE /api/onboarding/draft/documents/{key}?token | [AllowAnonymous] | — → 204 |
| A55 | GET /api/onboarding/applications | none | — → OnboardingQueueItemDto[] |
| A56 | GET /api/onboarding/applications/{id} | none | — → OnboardingReviewDto |
| A57 | POST /api/onboarding/applications/{id}/start-review | none | — → OnboardingReviewDto |
| A58 | POST /api/onboarding/applications/{id}/clarify | none | RequestClarificationRequest → OnboardingReviewDto |
| A59 | POST /api/onboarding/applications/{id}/approve | none | — → OnboardingApproveResultDto (promotes to master) |
| A60 | POST /api/onboarding/applications/{id}/reject | none | RejectOnboardingRequest → OnboardingReviewDto |
| A61 | POST /api/onboarding/draft/resubmit | [AllowAnonymous] | ResubmitOnboardingRequest → OnboardingApplicationDto |
| A62 | POST /api/onboarding/draft/raise-clarification | [AllowAnonymous] | RaiseClarificationRequest → OnboardingApplicationDto |

### PurchaseOrdersController — `api/pos`
| A63 | GET /api/pos | none | — → PoListItem[] |
| A64 | GET /api/pos/{id} | none | — → PoDetail |
| A65 | GET /api/pos/{id}/audit | none | — → AuditEntryDto[] |
| A66 | POST /api/pos/{id}/issue | none | — → PoDetail |
| A67 | POST /api/pos/{id}/acknowledge | none | — → PoDetail |

### RequisitionsController — `api/requisitions` (SourcingControllers.cs)
| A68 | GET /api/requisitions | none | — → RequisitionDto[] |
| A69 | GET /api/requisitions/{id} | none | — → RequisitionDto |
| A70 | POST /api/requisitions?submit | none | SavePrRequest → RequisitionDto |
| A71 | PUT /api/requisitions/{id} | none | SavePrRequest → RequisitionDto |
| A72 | POST /api/requisitions/{id}/submit | none | — → RequisitionDto |
| A73 | POST .../lines/{lineId}/cancel | none | ReasonRequest → RequisitionDto |
| A74 | POST .../lines/{lineId}/release | none | ReasonRequest → RequisitionDto |
| A75 | POST .../lines/{lineId}/reopen | none | — → RequisitionDto |
| A76 | POST .../lines/{lineId}/reserve | none | — → RequisitionDto |
| A77 | POST .../lines/{lineId}/unreserve | none | — → RequisitionDto |
| A78 | POST /api/requisitions/{id}/cancel | none | ReasonRequest → RequisitionDto |

### RfqsController — `api/rfqs` (SourcingControllers.cs)
| A79 | GET /api/rfqs | none | — → RfqListItem[] |
| A80 | GET /api/rfqs/{id} | none | — → RfqDetail |
| A81 | POST /api/rfqs | none | CreateRfqDraftRequest → RfqDetail |
| A82 | PUT /api/rfqs/{id} | none | UpdateRfqDraftRequest → RfqDetail |
| A83 | POST /api/rfqs/{id}/release | none | — → RfqDetail |
| A84 | POST /api/rfqs/{id}/close | none | — → RfqDetail |
| A85 | POST /api/rfqs/{id}/cancel | none | — → RfqDetail |

### FormsController — `api/forms` (SourcingControllers.cs)
| A86 | GET /api/forms | none | — → FormTemplateDto[] |
| A87 | GET /api/forms/{id} | none | — → FormTemplateDto |
| A88 | POST /api/forms | none | SaveFormTemplateRequest → FormTemplateDto |
| A89 | PUT /api/forms/{id} | none | SaveFormTemplateRequest → FormTemplateDto |
| A90 | DELETE /api/forms/{id} | none | — → 204 |

### StatementsController — `api`
| A91 | GET /api/statements | none | — → StatementSummaryDto[] |
| A92 | GET /api/statements/{vendorId} | none | — → StatementDetailDto |
| A93 | GET /api/my/statement | none | — → StatementDetailDto (vendor-scoped) |

### SwecController
| A94 | GET /api/swec | none | — → SwecCategoryDto[] |

### UsersController — `api/users`
| A95 | GET /api/users | none | — → UserDto[] |
| A96 | GET /api/users/vendor-logins | none | — → VendorLoginDto[] |
| A97 | POST /api/users | none | CreateUserRequest → UserDto |
| A98 | PUT /api/users/{id} | none | UpdateUserRequest → UserDto |

### VendorsController — `api/vendors`
| A99 | GET /api/vendors?q&type&region | none | — → VendorListItem[] |
| A100 | GET /api/vendors/{id} | none | — → VendorDetail |
| A101 | POST /api/vendors | none | CreateVendorRequest → VendorDetail (201) |
| A102 | POST /api/vendors/manual | none | CreateManualVendorRequest → ManualVendorResult |
| A103 | PUT /api/vendors/{id} | none | UpdateVendorRequest → VendorDetail |
| A104 | PUT /api/vendors/{id}/categories | none | SetCategoriesRequest → VendorDetail |
| A105 | POST /api/vendors/{id}/toggle-status | none | — → VendorDetail |
| A106 | GET /api/vendors/{id}/audit | none | — → AuditEntryDto[] |

**Global config** (Program.cs): JWT bearer (issuer/audience "eprocure", key from config, 1-min skew);
X-Demo-User header fallback (Auth/CurrentUser.cs:41-71); named role policies (never referenced by
endpoints — X1); CORS "dev-web" → localhost:5173; Swagger dev-only; `MigrateAsync()` + seeder on
startup (dev-only); no static-file/SPA hosting (Vite serves the web app).

## U. Frontend (hash-routing; role via X-Demo-User persona switcher)

Routing: `App.tsx` + `nav.ts` (buyer) + vendor routes via `VendorPortal`. Identity: `identity.tsx`
(`?as=` param / localStorage → X-Demo-User header; switch clears query cache).

| # | Screen (file) | Interactive elements | API calls |
|---|---|---|---|
| U1 | TopBar | role-switcher select (internal + vendor personas), profile chip | — (identity ctx) |
| U2 | Sidebar | nav items (keyboard accessible) | — |
| U3 | HealthBadge | status badge, 15s refetch | A37 |
| U4 | Dashboard | stat cards (some navigate); buyer: active-RFQ table + Open per row, New RFQ btn; vendor: invitation table w/ status-adaptive CTA (Start bid/Continue draft/Edit bid/View) | A22, A79, A9 |
| U5 | VendorMaster | search, type filter, region filter, row Open, New vendor btn, counts ribbon, SWEC label chips | A99, A94 |
| U6 | VendorDetail | 8 tabs (Overview/Categories/Contacts/Addresses/Banking/Compliance/Performance/Audit — audit lazy), Edit categories→SwecPicker, Deactivate/Reactivate | A100, A106, A104, A105 |
| U7 | ManualVendorForm | ~16 fields incl. dependent country→state→city dropdowns, currency/terms/bank lists, SWEC picker, dup-warning + Save anyway, actions top+bottom | A102, A16, A94 |
| U8 | SwecPicker modal | search, hierarchical checkbox tree, selected chips (click ✕), Cancel/Save | A94 |
| U9 | NewVendorChooser | Enter manually / Invite vendor choice cards | — |
| U10 | AdminUsers | Add user, users table + Edit per row, UserModal (name/email/role checkboxes — Vendor role excluded, SoD), vendor-logins read-only table | A95, A96, A97, A98 |
| U11 | AdminCustomLists | list rail selector, New list modal, values table (Edit modal: label/parent/sort/active; Delete), add-value row w/ dependent parent picker | A16, A18, A19, A20, A21 |
| U12 | Requisitions | New PR, table/board toggle, 6 facet filters + reset, row expand, line actions (Cancel/Release w/ reason modal, Reopen), group-select drawer → Create RFQ / Add to RFQ (picker mode) | A68, A81, A73, A74, A75 |
| U13 | PrForm | header fields, lines table (add/remove), Save draft / Submit / Cancel PR (reason modal), dirty-confirm | A69, A70, A71, A72, A78 |
| U14 | Consolidate | filters+search, expandable PR groups, per-line add (merge prompt: Combine / Keep separate; UoM mismatch auto-separate), basket w/ provenance sources + remove, Add all open, totals, Create RFQ | A68, A76, A77, A81 |
| U15 | RfqList | table/board toggle, New RFQ, per-status row actions (Continue / Close bids / Cancel w/ confirm; Open), bid-progress badges | A79, A84, A85 |
| U16 | RfqBuilder | 6–7-step wizard: items (add/remove/copy-from-PR), settings (title/envelope/currency/dates), questions (U18), vendors (search/filters/checkboxes/SWEC match), evaluators (dual only), review; Save draft / Release; save/update-form modals | A80, A86, A99, A95, A68, A82, A83, A88, A89 |
| U17 | RfqDetailHub | status-adaptive action bar (Close bids/Cancel/Open bids→openings/Proceed to award/View award), stat cards, invited-vendor bid table, confirm modals | A80, A84, A85 |
| U18 | QuestionEditor | commercial/technical groups, sections (add/rename/remove), items: 12 types + instruction + terms, drag reorder + up/down, per-item editor panel (label/required/help/config), move-across-sections | — (state via parent) |
| U19 | Forms | purpose filter (All/RFQ/Onboarding/Both), table w/ Edit/Duplicate/Delete per row, FormEditor (embeds U18) + Save/Delete | A86, A87, A88, A89, A90 |
| U20 | EvalScreens: openings list | closed-RFQ table, row → opening | A79 |
| U21 | BidOpening | technical + commercial envelope boxes (sealed counts, restricted note, gating copy), Open technical / Open commercial w/ authorization modal, continue-to-scoring | A29, A30, A31 |
| U22 | TechnicalEval | score matrix (vendor × evaluator × 4 criteria inputs), committee + pass/fail column, masked aliases, questionnaire answers table (incl. matrix/group render), finalize button + confirm | A32, A33, A34, A95 |
| U23 | AwardsList | ready-to-evaluate table + decisions table (status/approver/value/PO codes) | A79, A8 |
| U24 | AwardWorkspace | eligibility panel (tech-passed only), allocation grid (line × vendor qty inputs, recommended prefill, totals), Submit award, Approve modal (DoA), PO codes result | A4, A5, A6, A7 |
| U25 | PoList | 5 status tabs, stat cards, PO table w/ receipt progress bars | A63 |
| U26 | PoDetail | status-adaptive actions (Issue / Acknowledge (vendor) / Create invoice (vendor)), lines w/ received qty bars, audit trail expand, 3-way match banner | A64, A66, A67, A65 |
| U27 | DeliveryList | vendor: ready-to-ship POs + New shipping notice; ASN table; buyer: Receive per InTransit row | A23, A63 |
| U28 | AsnForm (vendor) | ship-plan lines (remaining prefill), per-line shipped qty + lot no, carrier, Create ASN confirm | A27, A28 |
| U29 | ReceiveForm (buyer) | per-line received qty (partial allowed), notes, Receive confirm → GRN, short-receipt handling | A24, A26 |
| U30 | AsnDetail | ASN info + lines, GRN summary if received | A24, A25 |
| U31 | InvoiceList | invoice table w/ match + status badges | A38 |
| U32 | InvoiceDetail | buyer actions (Approve for payment / Resolve exception w/ confirm modals), match detail per line (PO vs GRN vs INV qty, variance), NetSuite id display | A39, A42, A43 |
| U33 | InvoiceForm (vendor) | billable plan (qty/rate editable within caps), totals w/ SST/WHT, Submit confirm | A40, A41 |
| U34 | StatementList | stat cards (payable/GRNI/vendors), vendor table → detail | A91 |
| U35 | StatementDetail / VendorStatement | summary cards, aging buckets, running-balance ledger | A92 / A93 |
| U36 | Clarifications | thread list w/ unread badges, thread view, compose + broadcast toggle (buyer), New clarification modal (scope + counterpart) | A13, A14, A15 |
| U37 | ChatDock | draggable launcher w/ unread badge (15s poll), panel: list/thread/compose views, RFQ-context prefill | A13, A14, A15, A9, A79, A99 |
| U38 | VendorPortal | route dispatcher (bids/pos/deliveries/invoices/statement/chats/dashboard) | — |
| U39 | MyRfqs | invitation/bids table w/ status-adaptive CTA | A9 |
| U40 | BidForm (vendor) | line bidding (bid? checkbox, price, qty, alt item), questionnaire AnswerInputs (12 types incl. table matrix, repeatable group, attachments upload), Save draft / Submit confirm | A80, A10, A11, A12, A35, A36 |
| U41 | OnboardingQueue | applications table (status + round badges), Resend link per resendable row + magic-link modal, Review per row, New vendor | A55, A47 |
| U42 | OnboardingReview | Start review / Request clarification (batched items editor) / Reject (reason modal) / Approve→promote (actions top+bottom), company/banking/categories cards, financial band + Z-calc drawer, docs checklist, answers, rounds history | A56, A57, A58, A59, A60, A94 |
| U43 | OnboardingInvite | email/company/type, template checkboxes, doc checklist preview, Send invitation + magic-link modal | A44, A46 |
| U44 | OnboardingPortal + Landing | token resolve, welcome/checklist card, Start onboarding, invalid/expired error | A49 |
| U45 | OnboardingForm (vendor self-service) | stepper w/ amber warn-not-block states, company+geo (dependent lists), financials 11×3 grid w/ live Altman Z/band, docs upload/delete per checklist key, pack questions, SWEC picker, autosave on nav, submit enforcement | A50, A51, A52, A53, A54, A16, A94 |
| U46 | OnboardingResubmit | flagged-items-only editor, Resubmit all | A61 |
| U47 | Payments | **placeholder** — "coming soon" (deferred scope) | — |
| U48 | Catch-all Placeholder | unimplemented nav keys | — |

## D. Domain entities & enums (Domain project; EF config in AppDbContext.cs)

| # | Entity (file) | Essentials |
|---|---|---|
| D1 | AuditEntry | append-only; EntityType/EntityId/Action/Before/After/FromState/ToState/Reason/Actor; idx (EntityType,EntityId), UtcTimestamp |
| D2 | NumberSequence | composite PK (Prefix, Year); monotonic counter; AdvanceTo for seed alignment |
| D3 | User (Identity/User.cs) | Code+Email unique; Roles delimited; SetRoles rejects Vendor (SoD) |
| D4 | Vendor (+VendorContact/Address/BankAccount/Certification/Performance owned) | Code unique; Categories delimited SWEC codes; Rating/CreditLimit numeric(18,2) |
| D5 | VendorUser | separate principal; VendorId FK cascade; Code+Email unique |
| D6 | SwecCategory | string Code PK; tree (ParentCode, Level, IsLeaf, PathText) |
| D7 | PurchaseRequisition + PrLine | Code unique; HeaderStatus derived enum; PrLine.LifecycleStatus (Open/InDraftRfq/InRfq/Awarded/Cancelled/Closed); transitions are domain methods; DerivedValue not persisted |
| D8 | PrLineSourcing | append-only lineage fact (PrLineId→RfqId/RfqLineCode, QtySourced, LinkStatus Active/Returned/Cancelled, Reason, ClosedUtc); idx PrLineId/RfqId/RfqLineCode/LinkStatus |
| D9 | Rfq + RfqLine + FormItem | Code unique; Status enum; delimited PrRefs/InvitedVendorIds/evaluators; RfqLine.LineCode lineage target; FormItem = question/terms/instruction w/ ConfigJson |
| D10 | FormTemplate (+items) | Code unique; Purpose enum Rfq/Onboarding/Both |
| D11 | Bid (+BidLine/BidAnswer/BidAttachment) | UK (RfqId, VendorId); Submitted flag + SubmittedUtc; guarded by RFQ Open + ClosesUtc |
| D12 | TechnicalScore + TechnicalCriteria + TechnicalEvaluation | UK (RfqId,VendorId,EvaluatorId,Criterion); weights compliance 35/experience 20/delivery 20/qa 25; threshold 70; masked alias math |
| D13 | Award + AwardAllocation | Code unique; Draft/PendingApproval/Approved; ApproverUserId must differ (SoD/DoA) |
| D14 | PurchaseOrder + PoLine | PoStatus 8 states; ReceivedQty/InvoicedQty accumulators; Total computed |
| D15 | Asn + AsnLine | AsnStatus Draft/InTransit/Received; ship-qty clamps (rules [Q]) |
| D16 | Grn + GrnLine | received capped min(shipped, outstanding); Condition Good/Short |
| D17 | Invoice + InvoiceLine | InvoiceStatus 5 states; SstRate 8%, PriceTolerance 2%; qty capped to billable; totals computed server-side |
| D18 | Clarification | scope + vendor threads; per-party read state; idx (Scope, VendorId) |
| D19 | StoredFile | file bytes in DB (byte[]), name/contentType/size |
| D20 | CustomList + CustomListValue | list Code unique; value UK (CustomListId, Code); ParentValueCode dependent scoping; cascade |
| D21 | VendorOnboardingInvitation | TokenHash (SHA-256, unique) — raw never stored; constant-time Matches; Reissue/Open/Revoke/Expire/Complete transitions |
| D22 | VendorOnboardingApplication (+Answers/Documents/Steps/owned profile collections) | Code unique; 12-state OnboardingStatus machine, transitions throw DomainRuleException, return OnboardingTransition for audit; SubmittedUtc/DecisionUtc cycle-time |
| D23 | OnboardingClarificationRound + Item | append-only rounds; RoundNo per app; Respond fills once |
| D24 | VendorFinancialAssessment (+FinancialYearFigures ×3, FinancialSnapshot) | 11 decimal items/yr; snapshots AtSubmit/AtDecision write-once; LiveWeightedZ derived |
| D25 | AltmanZModel | pure static; coefficients 0.717/0.847/3.107/0.420/0.998; weights .2/.3/.5; bands A≥2.9/B≥2.0/C≥1.23/D; double math (JS parity) |
| D26 | DomainRuleException / Roles | 409 mapping; role constants |

**Enums:** RfqStatus, PrHeaderStatus, PrLineStatus, LinkStatus, BidStatus(flags), AwardStatus, PoStatus, AsnStatus, InvoiceStatus, FormPurpose, VendorType, OnboardingStatus(12), ApplicationSource, ClarificationDirection, ClarificationRoundStatus, OnboardingInvitationStatus, FinancialBand, RiskCategory, FinancialSnapshotStage — all stored as strings.

## M. Migrations (19)

M1 Slice00_Foundation (Audit, NumberSequences) · M2 Slice01_VendorsUsers · M3 Slice02_Sourcing ·
M4 Slice03_Bids · M5 Slice04_TechnicalEval · M6 Slice05_Award · M7 Slice07_Deliveries ·
M8 Slice08_Invoices · M9 Slice09Clarifications · M10 Slice10Files · M11 Slice11OwnersRecipients ·
M12 SliceAPrLineFoundation (**drops/re-adds PrLines.Id int→uuid, drops Status — seed-only data**) ·
M13 SliceCConsolidate (**drops RfqLines.Status**) · M14 SliceAVendorOnboarding ·
M15 SliceCOnboardingForm (FormTemplate.Purpose) · M16 SliceDPromotion (**drops staging cols**) ·
M17 SliceDReviewAnalyticsIndexes · M18 ReferenceLookups (superseded) ·
M19 CustomLists (**drops ReferenceItems**, creates CustomLists/Values).

## S. Cross-cutting services (Infrastructure)

S1 SmtpEmailSender (IEmailSender; logs when no SMTP host) · S2 OnboardingNotifier (templates;
TestRecipientOverride→vieshall@hypernix.net in dev; HTML-encodes; builds magic links) ·
S3 CodeGenerator (ICodeGenerator; NumberSequence + immediate SaveChanges) · S4 AuditLogWriter
(IAuditLog; Write/WriteTransition; append-only) · S5 AuditQuery · S6 SystemClock (IClock) ·
S7 FileStore (IFileStore; bytes in DB) · S8 NetSuiteClientStub (no-op logger) · S9 OnboardingService
(token hash/resolve, draft, review, clarify, approve→promotion, Altman snapshot) ·
S10 DevelopmentDataSeeder (idempotent full demo dataset + NumberSequence alignment) ·
S11 CurrentUser / X-Demo-User fallback auth (Api/Auth/CurrentUser.cs) · S12 ExceptionMiddleware
(DomainRule→409, NotFound→404, Forbidden→403) · S13 module services (Requisition, Rfq, Form, Bid,
Evaluation, Award, Po, Delivery, Invoice, Statement, Vendor, User, Swec, CustomList, Clarification,
Dashboard) · S14 SourcingMapping (free-text → dimension codes).

## X. Flags — orphans & mismatches (from Step 0 cross-referencing)

| # | Flag |
|---|---|
| X1 | **No endpoint-level authorization anywhere.** Zero `[Authorize]` on business controllers; no fallback policy (Program.cs:56 defines named policies that nothing references). Every endpoint A1–A106 except A37 is publicly callable without any identity. JWT/dev-login (A3) exists but nothing requires it. |
| X2 | Endpoint without UI: A48 revoke invitation — client fn `revokeOnboardingInvitation` exists (client.ts:283) but no button anywhere. |
| X3 | Endpoint without UI: A62 raise-clarification (vendor→buyer) — **no client function and no UI**; domain supports it (D22 RaiseVendorClarification). |
| X4 | Endpoint without UI: A45 GET onboarding/invitations — client fn exists, unused (queue uses A55). |
| X5 | Endpoint without UI: A101 POST /api/vendors — client fn `createVendor` (client.ts:131) unused; superseded by A102 manual. |
| X6 | Endpoint without UI: A1 GET dev-users — unused (`getDevUsers` client.ts:107; identity uses A2 personas). |
| X7 | Domain capability with no endpoint: Withdraw (D22), invitation Expire (D21 — likely internal on resolve; verify). |
| X8 | UI without backing feature: U47 Payments placeholder (documented as deferred — matches CLAUDE.md scope). |
| X9 | JWT dev-login flow (A3) exists but the web app never calls it — the SPA relies solely on X-Demo-User. |

## AUTH MATRIX — intended role vs actually enforced (verified 2026-07-02)

Endpoint attributes enforce nothing (X1). The only enforcement is service-layer `ICurrentUser`
checks. Verified by grep of every service: 7 services enforce something, **10 enforce nothing**.

| Module (endpoints) | Intended role (docs) | Actually enforced | Gap |
|---|---|---|---|
| Requisitions A68–A78 | Buyer | **nothing** (RequisitionService.cs: 0 checks) | any caller can create/cancel/release PRs |
| RFQs A79–A85 | Buyer | **nothing** (RfqService.cs: 0) | any caller can release/close/cancel RFQs |
| Forms A86–A90 | Buyer | **nothing** (FormService.cs: 0) | any caller can edit/delete templates |
| Vendors A99–A106 | Buyer/Admin | **nothing** (VendorService.cs: 0) | any caller can create/update/deactivate vendors |
| Users A95–A98 | Admin | **nothing** (UserService.cs: 0) | any caller can create users / grant roles |
| Custom Lists A16–A21 | Admin | **nothing** (CustomListService.cs: 0) | any caller can rewrite reference data |
| Onboarding buyer side A44–A48, A55–A60 | Buyer | **nothing** (OnboardingService.cs: 0 role checks) | any caller can approve/reject applications → writes to Vendor master |
| Onboarding token side A49–A54, A61–A62 | anonymous by design (token-scoped) | token hash resolve ✓ | correct |
| Files A35–A36 | authenticated (bid docs, bank letters) | **nothing** (FilesController/FileStore: 0) | unauthenticated upload + download by GUID |
| Bids A9–A12 | Vendor (own) | ✓ BidService.cs:91,104 (vendor + invited) | ok |
| Evaluation A29–A34 | Tech/CommEvaluator | ✓ EvaluationService.cs:29,53,122–131 | ok |
| Awards A4–A8 | Buyer + Approver (DoA) | partial — AwardService.cs:146 (Approver above threshold), :303 (internal only) | submit not Buyer-gated |
| POs A63–A67 | Buyer issue / Vendor ack | ✓ PoService.cs:23,38,60,73 (scoping + buyer-only issue) | ok |
| Deliveries A23–A28 | Vendor ship / Buyer receive | ✓ DeliveryService.cs:23,35,47,61,152 | ok |
| Invoices A38–A43 | Vendor submit / Buyer approve | ✓ InvoiceService.cs:23,36,44,163 | ok |
| Statements A91–A93 | Buyer list / Vendor own | ✓ StatementService.cs:38 (my-statement) | list not Buyer-gated |
| Clarifications A13–A15 | scoped both ways | ✓ vendor thread scoping | ok |
| Dashboard A22 | role-adaptive | ✓ DashboardService role branching | ok |
| Auth A1–A3, Health A37, SWEC A94 | open/dev | — | acceptable (A1 dev-only data) |

**X10 (new, Critical): X-Demo-User fallback is NOT environment-gated.** Api/Auth/CurrentUser.cs:53
runs the header fallback whenever there is no bearer token — in any environment. The XML doc
comment claims "In Development" but no `IHostEnvironment` check exists. Combined with X1, in a
deployed environment any caller can impersonate any seeded user (`X-Demo-User: u_admin`) or any
vendor login, defeating masking, SoD, and vendor scoping entirely.

**X11: No concurrency tokens anywhere** (no RowVersion/xmin in AppDbContext or Domain) — racing
writes (two evaluators scoring, draft autosave vs submit, double-approve) last-write-wins.

**X12: StoredFile keeps blobs as `byte[]` in Postgres** (D19, FileStore.cs) — DB bloat, backup
size, no streaming (whole file materialises in memory per request); 20 MB cap per file partially
mitigates. DBA/CTO lens item.

## FRONTEND → ENDPOINT MATRIX

All 28 client.ts base paths resolve to registered controller routes — **no client call targets a
missing route** (verified by path diff). Full per-screen call map is in the U-table above.
Orphans (endpoint exists, no UI): X2 revoke-invitation (recommend **wire** — a "Revoke" row action
in U41 queue; security-relevant), X3 raise-clarification (recommend **wire or remove** — domain +
endpoint shipped but vendors cannot reach it), X4 GET onboarding/invitations (recommend **remove**
client fn; queue supersedes), X5 POST /api/vendors createVendor (recommend **remove** client fn,
keep endpoint for API integrations or delete for surface reduction), X6 GET dev-users (recommend
**remove** client fn; personas supersedes).

## DRIFT CROSS-CHECKS vs docs bar

**(a) "Every entity has CreatedUtc/UpdatedUtc + decimal money + enum status" (DATA-MODEL.md) —
PARTIALLY TRUE.** Money ✓ (global numeric(18,2) convention; AltmanZ doubles are ratios, documented).
Enums ✓ (all stored as strings). Timestamps ✗ drift:
- No UpdatedUtc: Clarification, StoredFile, VendorOnboardingInvitation, Grn, PrLineSourcing (defensible — append-only), AuditEntry (defensible — UtcTimestamp).
- TechnicalScore has UpdatedUtc but **no CreatedUtc**.
- **String business dates** (analytics drift, DATA-MODEL-ANALYTICS requires typed): Asn.ShippedDate/ExpectedDate (Delivery.cs:17–18), Grn.ReceivedDate (:46), Invoice.Date (Invoice.cs:21), PR RaisedDate/RequiredDate (PurchaseRequisition.cs:37–38 — display-only duplicates; typed DateOnly RaisedOn/RequiredOn exist at :35–36 ✓).

**(b) "Department/Category/Location/Job carry stable codes not free-text" — IMPLEMENTED.**
PurchaseRequisition.cs:21–28 carries both display text AND *Code fields (DepartmentCode,
LocationCode, CategoryCode, JobCode), populated via SourcingMapping (S14). Verify population
completeness in Step 1 analytics lens.

## KNOWN BUGS pre-loaded for Step 2 verification

| # | Bug | Where |
|---|---|---|
| B1 | Consolidate card overlap on expand | U14 |
| B2 | Draft-PR lines shown as source-eligible | U12/U14 (eligibility should require Submitted) |
| B3 | Invite page ~8s load + Generate-link 404 | U43/A46 |

---

---

# STEP 1 — SIX-LENS REVIEW

Each finding: severity · file:line · fix · **[verified how]**. Verification legend:
**RUNTIME** = reproduced against the live app (curl/Playwright); **GREP** = confirmed by reading the
exact code; **CODE** = agent-reported from code read, spot-checked plausible; **DOC** = measured vs a spec.
Correction to Step 0: the analytics DBA pass showed M13/M16 only *add* columns in `Up()` — the truly
destructive migration is **M12 alone**, plus dead **M18→M19** churn (creates `ReferenceItems` then
drops it 2h later). The "3 destructive migrations" line in Step 0 is superseded by this.

## Lens 1 — DBA / Data Modeller

| # | Sev | Finding | Fix | Verified |
|---|---|---|---|---|
| DBA-1 | **Critical** | The entire P2P chain is FK-less: Award.RfqId, TechnicalScore.RfqId/VendorId, PrLineSourcing.PrLineId/RfqId, PO.VendorId/RfqId, Asn.PoId/VendorId, Grn.AsnId/PoId, Invoice.PoId/VendorId, Clarification.VendorId, Bid.RfqId/VendorId, plus onboarding refs — all "reference by id" with **no DB constraint** (only owned-collection + VendorUser→Vendor + CustomListValue→CustomList + 2 onboarding FKs are real). AppDbContext.cs:340-414. A bad insert (realistic given X1) silently orphans audited award/payment rows. | One migration adding `HasOne<T>().WithMany().HasForeignKey(...).OnDelete(Restrict)` for each pair; keep aggregate boundaries (no nav property needed). | GREP (model snapshot) |
| DBA-2 | **Critical** | No concurrency tokens anywhere (no RowVersion/xmin). PoLine.ReceivedQty/InvoicedQty are read-modify-write accumulators (DeliveryService.cs:118, InvoiceService.cs:80) — a race defeats the 3-way-match caps; double-approve award → duplicate POs; onboarding autosave-vs-submit. | `UseXminAsConcurrencyToken()` on Rfq, Bid, Award, PurchaseOrder, Invoice, Asn, VendorOnboardingApplication + map DbUpdateConcurrencyException→409. | GREP (0 hits) |
| DBA-3 | **High** | "Append-only" is a C# private-setter promise only — nothing at DB level stops UPDATE/DELETE of AuditEntries / FinancialSnapshots / clarification rounds. No trigger, no REVOKE. AuditEntry.cs:10-13, AppDbContext.cs:17. Golden rule is an *immutable* trail. | Postgres BEFORE UPDATE/DELETE trigger `RAISE EXCEPTION` on those tables, or run app under a role with REVOKE UPDATE,DELETE. | GREP (no `migrationBuilder.Sql`) |
| DBA-4 | **High** | No uniqueness on Awards.RfqId though AwardService.cs:113 assumes 1:1 (`FirstOrDefault(a.RfqId==)`). A submit race stores two awards → two PO sets. | `HasIndex(x=>x.RfqId).IsUnique()`. | CODE |
| DBA-5 | **High** | Seven line tables (AwardAllocation, PoLine, AsnLine, GrnLine, InvoiceLine, RfqLine, BidLine) use EF ordinal composite PKs, not stable Guid surrogates; RfqService.cs:85 & AwardService.cs:123 replace collections wholesale on save → ordinals churn. Fact tables can't key on them. | Add `Guid Id` to each line entity (the proven M12 PrLine playbook). | CODE (migrations) |
| DBA-6 | **High** | Missing indexes on hot P2P predicates: Invoice.PoId/VendorId/Status, Grn.AsnId/PoId, Asn.PoId/VendorId, PO.VendorId/RfqId/Status, PO.AwardCode (string-join!), Rfq.Status, AuditEntry.ActorId. Every list/statement query seq-scans. | One index migration; add `AwardId` FK to PO and stop joining on AwardCode. | GREP (snapshot) |
| DBA-7 | **High** | String business dates beyond ASN/GRN/Invoice: **VendorCertification.ValidTo : string** (Vendor.cs:88) — "expiring certs" report (real O&G compliance need) impossible in SQL; onboarding promotes into the same shape. | Additive `DateOnly` columns (ShippedOn/ExpectedOn/ReceivedOn/InvoiceDate/ValidToOn), backfill, UI keeps formatting. | GREP |
| DBA-8 | **Med-High** | `Rfq.InvitedVendorIds` delimited string drives bid-scoping, eligibility AND positional bidder masking (`TechnicalEvaluation.Alias` = index in list) — reorder/edit silently re-aliases bidders mid-eval. No FK, not queryable, no dedup. Evaluator ids reference users by **mutable string code**. | Promote to `RfqInvitation(RfqId, VendorId, Ordinal)` + `RfqEvaluator` join tables. | CODE |
| DBA-9 | **Med** | CodeGenerator check-then-act race (CodeGenerator.cs:20-30): two concurrent creators get the same Code; unique index turns it into a 500, not a clean retry. | `SELECT…FOR UPDATE`, native Postgres sequence, or xmin+retry. | CODE |
| DBA-10 | **Med** | `Award.TotalValue` persisted aggregate (drift vs allocations) used for the DoA gate; PoLine received/invoiced accumulators duplicate GRN/Invoice facts with no reconciliation. §6 wants derived-not-stored. | `Ignore()` Award.TotalValue (compute like PO.Total); reconcile or derive accumulators. | GREP |
| DBA-11 | **Med** | `BidAttachment` holds only FileName; the StoredFile link lives inside answer text — orphaned files undetectable, no FK. OnboardingDocument models it right but its StoredFileId has no FK. | Add `BidAttachment.StoredFileId` + FK; add OnboardingDocument.StoredFileId FK. | GREP |
| DBA-12 | **Low** | `VendorOnboardingInvitations.TokenHash` index is **not unique** (AppDbContext.cs:234) though D21 claims unique; also `Rfq.ClosesUtc` nullable with no check-constraint that an Open RFQ has a deadline; free-string enums (GrnLine.Condition, Clarification.SenderKind, FormItem.Kind/Group/Type, ReviewStep.Outcome) unconstrained; TechnicalScore has UpdatedUtc but no CreatedUtc. | `.IsUnique()`; add check constraints; store as enums-as-string. | GREP |

Where reality meets the bar (DBA): global numeric(18,2); enums-as-strings; surrogate Guid + unique
business Code on every aggregate; PrLineSourcing append-only w/ 4 lineage indexes; typed AuditEntry
transition columns; owned-collection cascades correct; every migration has a plausible Down().

## Lens 2 — Analytics Engineer (vs DATA-MODEL-ANALYTICS)

| # | Sev | Finding | Verified |
|---|---|---|---|
| AN-1 | **Critical (bug)** | **GRN.ReceivedDate is the hardcoded literal `"28/06/2026"`** (DeliveryService.cs:125). Every GRN posted through Receive records a fixed, false business date shown on U29/U32 match detail — a functional defect, not just analytics. | RUNTIME (grep + endpoint) |
| AN-2 | **High** | Lineage breaks at Award→PO: `PO.AwardCode` string join (AwardService.cs:311, "never join on codes" §3), no `AwardId`; PoLine carries no AllocationId/PrLineId; downstream ASN/GRN/Invoice reconcile by ItemCode string match. PR↔RFQ half and onboarding chain ARE intact. | CODE |
| AN-3 | **High** | Transition timestamps missing across RFQ/Award/PO/Invoice: no ReleasedUtc/IssuedUtc/AcknowledgedUtc/ReceivedUtc/ApprovedUtc/PaidUtc; early close **overwrites** planned ClosesUtc (RfqService.cs:131) conflating planned vs actual; envelope open/finalize are bare bools. These services call `WriteAsync` with prose, not `WriteTransitionAsync` — cycle-time needs text parsing. | CODE |
| AN-4 | **High** | VendorPerformance (OTD/Quality/SpendYTD/WinRate) is seeded once, **never recomputed** (Vendor.cs:48-58; only read) — permanently-stale stored aggregate presented as live (U6 tab, vendor list OTD). §6 drift. | CODE |
| AN-5 | **High** | Vendor geography is dual-vocabulary: seeded vendors store labels ("Malaysia","30 days nett","Malayan Banking Berhad", VendorSeed.cs:76), manual/onboarded store codes ("MY","NET30","MBB"). `GROUP BY Country` splits Malaysia into two buckets **today**. | GREP |
| AN-6 | **High** | FormTemplate DELETE (A90) & CustomListValue DELETE (A21) are hard deletes with no in-use check — orphan onboarding answers / vanish a dimension row. Both entities already have an `Active` flag (the intended soft path). | GREP |
| AN-7 | **Med** | Dept/Location/Job codes are slugified free text (SourcingMapping.cs:21-25) — "Maintenance"/"Maint." → different codes; no reference table (SWEC has one, these don't). Conformed-*on-PR* but drift-prone. | CODE |
| AN-8 | **Med** | RfqLine.LineCode defaults to ItemCode (SourcingMapping.cs:64) — two same-item "kept separate" lines collide → ambiguous PrLineSourcing.RfqLineCode; AwardService.cs:169 `First(ItemCode==)` can pick wrong line. | CODE |
| AN-verdict | — | **Star/view layer with zero business-table refactor: NO.** PR↔RFQ + onboarding are analytics-ready; the award→pay half is not. Blockers: no stable line keys (DBA-5), broken award→PO lineage (AN-2), missing transition timestamps (AN-3), string dates (DBA-7). All fixes additive. | DOC |

## Lens 3 — CTO (security / reliability / scale) — **runtime-confirmed risk register**

| # | Sev | Finding | Verified |
|---|---|---|---|
| SEC-1 | **Critical** | **No endpoint authorization + X-Demo-User works.** `GET /users`, `/vendors`, `/rfqs`, `/onboarding/applications` return **200 with full data** unauthenticated; `X-Demo-User: u_admin` → 200. Program.cs role policies referenced by nothing; CurrentUser.cs:53 header fallback has **no `IHostEnvironment` gate** (only IsDevelopment use is Program.cs:93 for migrate/swagger). | **RUNTIME** |
| SEC-2 | **Critical** | Sensitive **mutations reachable unauthenticated**: `POST /onboarding/applications/{id}/approve` → 404 (reached logic, not 401) — an anon caller can promote a vendor into the master; `POST /users` → 400 (validation, not 401) — reached user-creation logic. Only business/state guards stand in the way. | **RUNTIME** |
| SEC-3 | **Critical** | **PII/banking leak.** Unauthenticated `GET /vendors/{id}` returns bankAccounts incl. accountNo/SWIFT; `GET /files/{id}` is ungated (random GUID → 404 not 401, so a valid GUID downloads bank letters / financials / sealed bid docs). | **RUNTIME** |
| SEC-4 | **High** | 10 of 16 services enforce nothing (RequisitionService, RfqService, FormService, VendorService, UserService, CustomListService, OnboardingService buyer-side, FileStore, + Audit/Swec). Even if `[Authorize]` is added at controllers, direct/service calls have no guard. Bid/Eval/PO/Delivery/Invoice/Statement DO enforce vendor-scope/roles (verified working: vendor sees only own POs). | GREP + RUNTIME |
| SEC-5 | **High** | FileStore.SaveAsync: no content-type allowlist, no filename sanitisation, size cap only at controller (`[RequestSizeLimit]`, bypassable service-side), blobs in DB. | CODE |
| SEC-6 | **Med** | Magic-link token is strong (24-byte RNG, SHA-256 hashed, expiry+scope enforced on resolve, constant-time compare — all good), but `Reissue()` does not invalidate the previous token hash; no upload rate-limit per token. | CODE |
| SEC-7 | **Med** | No masking on bank/SWIFT/financials for any reader; financials stored plaintext. CORS is localhost-only but `UseCors` is applied unconditionally (Program.cs:110) — tighten with env guard + security headers before deploy. | CODE |
| SEC-8 | **Low/good** | Dependencies are stable (.NET 10.0.9, EF 10.0.9, React 19) — no preview/RC. JWT key is an obvious dev placeholder in appsettings.Development.json (not prod). ExceptionMiddleware maps 409/404/403 cleanly and logs 500s without leaking traces. OnboardingNotifier HTML-encodes. | GREP |
| REL-1 | **Med** | StoredFile byte[] blobs in Postgres (TOAST bloat, WAL/backup size, whole-file-in-memory downloads). Fine for demo; not at prod bar. Move to object storage, stream responses. | CODE |
| REL-2 | **Low/good** | Reported ~8s Invite load did **not** reproduce (837ms); email send is not blocking the invite response in practice. No N+1 found in ListApplications (Rounds eagerly included). | RUNTIME |

## Lens 4 — Solution Architect

| # | Sev | Finding | Verified |
|---|---|---|---|
| ARC-1 | **High** | Anemic-domain drift: **17** direct `.Status =` writes in services bypass domain methods (RfqService.cs:116/130/144 Open/Closed/Cancelled; AwardService.cs:121/178/182; DeliveryService, InvoiceService, PoService, EvaluationService). Onboarding + PrLine correctly use rich transition methods — the rest don't. ENGINEERING-STANDARDS §1 wants transitions as aggregate methods. | GREP (17 hits) |
| ARC-2 | **High** | `Vendor.Status` is `{ get; set; }` public (Vendor.cs:23) — any caller sets Registered/Blacklisted with no lifecycle guard. Same for public `Code` setters on Rfq/Award (should be private/ctor). | GREP |
| ARC-3 | **Med** | Award eligibility (technically-passed) is recomputed live from scores at award time, not snapshotted at TechFinalized — editing a score retroactively changes historical eligibility. | CODE |
| ARC-4 | **Med** | Onboarding approval writes vendor+user+assessment+invitation in one SaveChanges but **no explicit transaction/isolation**; duplicate check is read-only before the write (race → duplicate vendor). Add DB transaction + unique constraint on (RegistrationNo). | CODE |
| ARC-5 | **Med** | OnboardingAnswer vs BidAnswer are near-identical (FormTemplateId/QuestionOrder/Value) — extract a shared value object. FormTemplate/FormItem, StoredFile, NumberSequence, AuditEntry, VendorUser reuse is otherwise good. | CODE |
| ARC-6 | **Low** | OnboardingReviewStep exists as a workflow seam but is vestigial (hardcoded steps, no engine) — fine as a seam, don't mistake it for implemented. | CODE |

Where reality meets the bar (Architect): Domain has no EF dependency; controllers are thin;
FormTemplate/StoredFile/NumberSequence/AuditEntry genuinely reused across sourcing+onboarding;
aggregates are sensibly bounded.

## Lens 5 — Senior Programmer

| # | Sev | Finding | Verified |
|---|---|---|---|
| PRG-1 | **High** | Magic strings/numbers: TechnicalScore criterion keys, status literals in service `.Status=` sites. SST 0.08 & tolerance 0.02 ARE named consts (Invoice.cs:7-8, good); Altman coefficients ARE named (AltmanZModel, good). | GREP |
| PRG-2 | **Med** | `BidAnswer.QuestionOrder`/`OnboardingAnswer.QuestionOrder` key answers by **position** into FormItem.Order — DATA-MODEL.md:37 specifies `FormItemId`. Editing a draft's items re-keys saved answers. Give FormItem a stable id. | GREP |
| PRG-3 | **Med** | Onboarding email send (OnboardingService) is awaited inline, not wrapped — an SMTP failure would surface on the request; today SMTP host is blank so it logs (dev). Wrap + warn on failure. | CODE |
| PRG-4 | **Low** | 4 orphan client fns (createVendor, getDevUsers, getOnboardingInvitations, revokeOnboardingInvitation). Dead surface. | GREP |
| PRG-5 | **Low/good** | **IClock is used consistently — 0 `DateTime.UtcNow`/`DateTime.Now` outside SystemClock** (the agent's "IClock not enforced" claim is FALSE). async/CancellationToken threading is consistent. TanStack invalidation is correct on the mutations spot-checked. | GREP (0 hits) |
| PRG-6 | **Low** | Build/lint status at audit time: `dotnet test` 218/218, `vitest` 90/90, `tsc -b` clean, oxlint 0 errors (pre-existing fast-refresh warnings only). | RUNTIME |

## Lens 6 — Procurement Buyer (usability, via live walkthrough — screenshots in ./screenshots)

- **Strong:** every buyer + vendor route renders with seeded data and **zero JS/page errors** (26/26 crawl); role-adaptive dashboards work for Buyer/Admin/Approver/Tech/Vendor; status-adaptive action bars match procurement mental model (Close bids→Open bids→Proceed to award); vendor scoping means a supplier sees only its own POs. B1/B2/B3 (the three reported bugs) are **all fixed**.
- **BUY-1 (High, = AN-1):** GRN receipt date always shows 28/06/2026 — a buyer receiving goods sees a wrong, fixed date on the match detail. Confusing and audit-breaking.
- **BUY-2 (Med):** Deadlines render in the browser's local timezone with no "UTC" label — a buyer in UTC+8 misreads an RFQ close time by 8h (dateToIso writes correct UTC; display doesn't label it).
- **BUY-3 (Med):** Payments is a visible nav item that only shows "coming soon" (U47) — documented deferral, but it's a dead click in the primary nav.
- **BUY-4 (Low):** No "Revoke invitation" affordance though the endpoint+domain exist (X2) — buyer can resend but not revoke a mis-sent link.

---

# STEP 2 — UI FEATURE TEST RESULTS (Playwright, real Chromium)

Setup: installed `@playwright/test` + Chromium in isolated `e2e-audit/` (no app package.json touched);
drove `http://localhost:5173` via `?as=<persona>` identity; API :5260 + web :5173 both live.
**Totals: 35/35 Playwright checks passed** (26 route-crawl + 3 known-bug + 6 functional).
Automated suites: **API dotnet test 218/218**, **web vitest 90/90**. Screenshots: `./screenshots/` (38).

| Area (Feature Map) | Test | Result | Evidence |
|---|---|---|---|
| U4 dashboards ×5 roles | render, no JS error | PASS | U4-dashboard-{buyer,vendor,admin,approver,tech}.png |
| U5–U11 buyer setup screens | load+console/net capture | PASS | U5,U10,U11 shots |
| U12–U19 sourcing (reqs/consolidate/rfqs/forms) | load | PASS | U12,U14,U15,U19 shots |
| U20–U35 eval/award/po/delivery/invoice/statement | load ×2 roles | PASS | U20–U35 shots |
| U36/U41–U47 clarifications/onboarding/payments | load | PASS | U36,U41,U43,U47 shots |
| B1 Consolidate expand overlap | bounding-box adjacency | **PASS (fixed)** | B1-consolidate-expanded.png |
| B2 Draft-PR source eligibility | Draft hidden / Submitted shown | **PASS (fixed)** | B2-consolidate-left-pane.png |
| B3 Invite load + generate link | timing + magic-link resolves | **PASS (fixed)** — 837ms load, 82ms gen, link resolves (no 404) | B3-invite-*.png |
| Vendor Master search/filter | narrows list | PASS | F-vendor-search.png |
| Manual vendor validation | blocks empty name | PASS | F-manual-vendor-validation.png |
| Admin Custom Lists add value | round-trips + cleanup | PASS | F-custom-list-add.png |
| Onboarding Review Altman-Z band | financial section renders | PASS | F-onboarding-review.png |
| Technical evaluation | matrix renders | PASS | F-bid-openings.png |
| Vendor PO scoping (guard [G]) | vendor rows ≤ buyer rows | PASS | F-vendor-po-scope.png |
| **Security probes (API, no auth)** | should be 401, are not | **FAIL (vuln confirmed)** | /users,/vendors,/rfqs,/onboarding 200; approve 404; files ungated |

Notes: Altman-Z numeric correctness is covered by API unit tests (OnboardingFinancialModelTests, in the
218) + on-screen band render verified here — not re-derived in the browser. No 4xx/5xx or >3s API calls
were observed on any page load during the crawl.

Features with **NO automated test** (gap): RfqBuilder, RfqList, RfqDetailHub, VendorDetail,
VendorMaster (has crawl coverage only), EvalScreens (only TechnicalEval unit-tested), MyRfqs,
VendorPortal, OnboardingInvite, OnboardingQueue, Dashboard, Clarifications, TopBar identity switch,
and — most important — **zero tests assert authorization** (because there is none to assert).

---

# STEP 3 — GAP REPORT

### (a) Prioritized defect list
1. **[Critical] Unauthenticated API + X-Demo-User impersonation** (SEC-1/2/3) — CurrentUser.cs:53, all controllers. Repro: `curl localhost:5260/api/users` → 200; `curl -H 'X-Demo-User: u_admin' …/users` → 200; `curl -X POST …/onboarding/applications/<guid>/approve` → 404 not 401.
2. **[Critical] File download & vendor bank data open to anonymous** (SEC-3) — FilesController.cs:21, VendorsController. Repro: `GET /api/vendors/{id}` returns SWIFT/accountNo unauthenticated.
3. **[High/bug] GRN receipt date hardcoded "28/06/2026"** (AN-1/BUY-1) — DeliveryService.cs:125. Repro: post any GRN via Receive → detail shows 28/06/2026.
4. **[High] No FK integrity across P2P** (DBA-1) — orphan risk on audited award/payment rows.
5. **[High] No concurrency tokens** (DBA-2) — lost updates on 3-way-match accumulators / double-approve.
6. **[High] DB-unenforced append-only** (DBA-3) — audit/snapshot rows mutable via any code path.
7. **[High] 10/16 services enforce no authz** (SEC-4) — even post-`[Authorize]`, no defence in depth.
8. **[High] Anemic status mutation** (ARC-1) — 17 direct `.Status=` writes bypass domain guards.
9. **[High] VendorPerformance never recomputed** (AN-4) — stale OTD/spend shown as live.
10. **[High] Award→PO lineage on string code** (AN-2) + 7 ordinal line PKs (DBA-5).

### (b) Spec/requirement gaps
- Immutable audit trail (BUSINESS-RULES) is code-enforced, **not DB-enforced** (DBA-3).
- DATA-MODEL "every entity CreatedUtc/UpdatedUtc": drift (TechnicalScore no CreatedUtc; string dates on ASN/GRN/Invoice/Cert vs DATA-MODEL-ANALYTICS §5).
- Conformed dimensions implemented on PR (good) but **not propagated to downstream facts** and vendor geography is dual-vocabulary (AN-5).
- Segregation of Duties / DoA: Award has the DoA check (AwardService.cs:146) but it's bypassable because the endpoint isn't authorized (SEC-2) — the guard rail is real in code, unreachable-safe in deployment.
- BidAnswer keyed by position not FormItemId (PRG-2) vs DATA-MODEL.md:37.

### (c) Missing tests
Authorization (none exist / none enforced), RfqBuilder, EvalScreens flow, VendorDetail tabs, OnboardingInvite/Queue, Dashboard, identity switching, and any WebApplicationFactory HTTP-level integration test (all current API tests are service-level).

### (d) Analytics-readiness gaps
Stable line-level Guid keys (DBA-5), Award→PO/line lineage keys (AN-2), transition timestamps + typed audit for RFQ/Award/PO/Invoice (AN-3), typed business dates (DBA-7), dimension propagation + dedup vocabulary (AN-5/7). Verdict: **cannot build a star schema with zero business-table refactor today** — but every fix is additive.

### (e) Security gaps
SEC-1..7 above. Headline: authentication is effectively optional in the deployed shape.

### (f) UX gaps
GRN date bug (BUY-1), unlabelled timezone on deadlines (BUY-2), Payments dead nav (BUY-3), no revoke-invitation (BUY-4). Otherwise the UI is clean, populated, and error-free across all routes.

### (g) Quick wins vs larger efforts
- **Quick (<1 day each):** env-gate X-Demo-User (SEC-1, ~15min); add `[Authorize]` + policies to controllers (SEC-1/2, ~2h); authorize FilesController + vendor detail (SEC-3, ~1h); fix GRN date to `clock.UtcNow` (AN-1, ~15min); `Award.RfqId` unique + Ignore Award.TotalValue (DBA-4/10); env-gate CORS (SEC-7); delete 4 orphan client fns (PRG-4); UTC label on dates (BUY-2).
- **Medium (days):** FK migration (DBA-1); xmin concurrency + 409 mapping (DBA-2); service-layer role guards (SEC-4); domain transition methods to kill the 17 `.Status=` (ARC-1); VendorPerformance recompute/derive (AN-4); soft-delete forms/list-values (AN-6); file upload validation (SEC-5).
- **Larger (weeks):** stable line-Guid + downstream lineage keys (DBA-5/AN-2); typed dates backfill (DBA-7); object storage for files (REL-1); DB-level append-only triggers (DBA-3); junction tables for delimited multi-values (DBA-8).

### Executive summary
eProcure is a genuinely well-built application by modelling and UX discipline: clean architecture with
an EF-free domain, rich onboarding/PR state machines, conformed dimension codes on requisitions,
write-once financial snapshots, consistent IClock/CodeGenerator/AuditEntry usage, and a polished,
error-free UI that renders every one of ~48 screens across five roles with seeded data. The automated
suites are green (218 API + 90 web) and — notably — the three previously-reported bugs (Consolidate
overlap, Draft-line eligibility, Invite slow-load/404) are all fixed on this build. The critical gap is
**not features, it's the security perimeter**: there is no endpoint authorization and the X-Demo-User
demo shim is not environment-gated, so in a deployed configuration any anonymous caller can read vendor
bank details, download any file, create users, and approve vendors into the master — I confirmed each at
runtime. Beneath that sit two systemic hardening gaps the docs themselves demand: the database enforces
almost no referential integrity, concurrency control, or append-only immutability (it's all promised in
C#), and the award→pay half of the lineage isn't analytics-ready (string-code joins, ordinal line keys,
missing transition timestamps, one hardcoded GRN date). None of the data-layer fixes require reshaping
existing readers — they are additive. Fix the perimeter first; it is roughly a day of work and it is the
only thing standing between "impressive demo" and "safe to deploy."

### Top-10 "fix first"
1. Env-gate the X-Demo-User fallback (CurrentUser.cs:53) — 15 min, closes prod impersonation.
2. Add `[Authorize]` + role policies to every business controller — ~2 h.
3. Authorize FilesController download + vendor bank/financial reads (mask for non-privileged) — ~1–2 h.
4. Add service-layer role/scope guards to the 10 unguarded services (defence in depth) — ~3 h.
5. Fix GRN hardcoded receipt date → `clock.UtcNow` + typed DateOnly (DeliveryService.cs:125) — 15 min.
6. Add the FK-integrity migration across the P2P chain (DBA-1) — ~half day.
7. Add xmin concurrency tokens + 409 mapping on the mutable/accumulator aggregates (DBA-2) — ~half day.
8. Replace the 17 direct `.Status=` writes with domain transition methods (ARC-1) — ~1 day.
9. Recompute/derive VendorPerformance instead of showing seeded-stale values (AN-4) — ~half day.
10. Unique index on Award.RfqId + wrap onboarding approval in a transaction (DBA-4, ARC-4) — ~2 h.

