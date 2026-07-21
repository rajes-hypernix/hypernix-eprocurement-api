import { Icon } from "@/components/Icon";

/**
 * New Vendor chooser — manual entry or invite onboarding.
 */
export function NewVendorChooserPage({
  onManual,
  onInvite,
  onBack,
}: {
  onManual: () => void;
  onInvite: () => void;
  onBack: () => void;
}) {
  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Vendor Master
        </button>{" "}
        <Icon name="chev" size={12} /> New Vendor
      </div>
      <div className="pagehead">
        <div>
          <h1>New Vendor</h1>
          <p>Add a supplier yourself, or invite them to complete onboarding through the portal.</p>
        </div>
      </div>

      <div className="choose">
        <button type="button" className="choice" onClick={onManual}>
          <div className="ci">
            <Icon name="edit" size={22} />
          </div>
          <h3>Enter manually</h3>
          <p>
            You key in the supplier’s details now. Added straight to the master — no approval step.
            Best for a known supplier you’re setting up quickly.
          </p>
        </button>
        <button type="button" className="choice" onClick={onInvite}>
          <div className="ci">
            <Icon name="send" size={22} />
          </div>
          <h3>Invite vendor</h3>
          <p>
            Send a secure link. The vendor fills their own profile, financials and your selected
            questions, then it routes to you to review and approve.
          </p>
        </button>
      </div>
    </>
  );
}
