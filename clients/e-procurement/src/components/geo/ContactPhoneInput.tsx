import PhoneInput, { isSupportedCountry, type Country, type Value } from "react-phone-number-input";
import flags from "react-phone-number-input/flags";
import "react-phone-number-input/style.css";

const FALLBACK_COUNTRY: Country = "MY";

export function toPhoneCountry(iso2?: string | null): Country {
  const code = (iso2 ?? FALLBACK_COUNTRY).trim().toUpperCase();
  return isSupportedCountry(code) ? (code as Country) : FALLBACK_COUNTRY;
}

export function ContactPhoneInput({
  countryCode,
  value,
  onChange,
  id = "contact-phone",
}: {
  countryCode?: string | null;
  value: string;
  onChange: (e164: string) => void;
  id?: string;
}) {
  const defaultCountry = toPhoneCountry(countryCode);
  const phone = (value?.trim() || undefined) as Value | undefined;

  return (
    <PhoneInput
      key={phone ? "contact-phone" : `contact-phone-${defaultCountry}`}
      international
      countryCallingCodeEditable={false}
      defaultCountry={defaultCountry}
      flags={flags}
      value={phone}
      onChange={(next) => onChange(next ?? "")}
      numberInputProps={{ id, autoComplete: "tel" }}
    />
  );
}
