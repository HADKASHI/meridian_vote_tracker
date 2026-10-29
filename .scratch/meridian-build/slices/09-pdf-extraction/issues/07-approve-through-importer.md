# 07: Approve an extraction through the importer

**What to build:** Approving a clean extraction makes an import of the sheet's rows that follows the CSV's own rules: new, unchanged, duplicate, invalid, contradiction or conflict, and nothing overwrites approved data without a person deciding.

**Blocked by:** Edit and reject an extraction

**Status:** ready-for-agent

**Requirements:** PDF-05

- [ ] Approve is refused while a blocking issue remains
- [ ] Approving creates one import that points at the stored PDF without copying it and writes through the existing import writer, so the report, history and origins work as for a CSV
- [ ] A first sheet for a vote gives new votes; the same sheet again gives unchanged
- [ ] The corrigendum fixture after the original gives exactly one conflict for the changed state and leaves approved data as it was
- [ ] A second approve is refused with 409
- [ ] The imports list and report tolerate a PDF file whose rows are numbered by sheet order
