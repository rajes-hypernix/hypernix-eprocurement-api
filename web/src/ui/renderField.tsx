import type { ChromeMode, FieldSpec } from './fieldSpec'
import { TextField } from './TextField'
import { TextAreaField } from './TextAreaField'
import { CodeField } from './CodeField'
import { SelectField } from './SelectField'
import { MultiSelectField } from './MultiSelectField'
import { YesNoField } from './YesNoField'
import { SegmentedField } from './SegmentedField'
import { NumberField } from './NumberField'
import { MoneyField } from './MoneyField'
import { DateField } from './DateField'
import { AttachmentField } from './AttachmentField'
import { TableField } from './TableField'
import { RepeatingGroupField } from './RepeatingGroupField'

/**
 * The one dataType → primitive dispatch (generalised from PrForm's D1 local
 * dispatcher). Archetype sections render whole FieldSpec arrays through this;
 * D5 custom fields ride the same switch. DependentSelectField and
 * CheckboxField are NOT dispatched here — the former needs the render site's
 * parentValue, the latter a boolean value; both are composed explicitly.
 */
export function renderField(
  spec: FieldSpec,
  value: string,
  onChange: (v: string) => void,
  opts: { chrome?: ChromeMode; error?: string } = {},
) {
  const p = { key: spec.key, spec, value, onChange, chrome: opts.chrome, error: opts.error }
  switch (spec.dataType) {
    case 'longText': return <TextAreaField {...p} />
    case 'code': return <CodeField {...p} />
    case 'select': return <SelectField {...p} />
    case 'multiSelect': return <MultiSelectField {...p} />
    case 'yesNo': return <YesNoField {...p} />
    case 'segmented': return <SegmentedField {...p} />
    case 'number': case 'percent': return <NumberField {...p} />
    case 'money': return <MoneyField {...p} />
    case 'date': case 'dateTime': return <DateField {...p} />
    case 'attachment': return <AttachmentField {...p} />
    case 'table': return <TableField {...p} />
    case 'group': return <RepeatingGroupField {...p} />
    default: return <TextField {...p} />
  }
}
