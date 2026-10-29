# 05: Review inbox and decision pane

**What to build:** the Review inbox matches the prototype's split pane: filter chips (pending, decided, all), a conflict list showing vote, subject chip, current → incoming and import, with the selected row marked, beside a decision pane with the current and incoming values in two cards with their origins, the comment field and Accept incoming / Keep current. A decision shows as a card; the 409 shows the already-decided card in place of the buttons. On a phone the list and the pane stack.

**Blocked by:** 03

**Status:** ready-for-agent

- [ ] Filter chips with the active one marked; "Only some conflicts are shown" notice with Show all
- [ ] Conflict list in a card, rows with subject chip and the from → to display, selected row marked, empty state
- [ ] Decision pane: breadcrumb-free title with the vote link, value cards with origins, comment field and actions
- [ ] Decided state and membership recalculation shown as a card; already-decided card in place of the buttons
- [ ] List and pane stack below a tablet width; nothing scrolls sideways at 390 px
- [ ] Existing review and conflict tests pass
