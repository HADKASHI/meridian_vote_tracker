# Define import classification rules

Type: grilling
Status: open
Blocked by: 04, 07

## Question

What exact algorithm gives each row exactly one outcome of new, unchanged, duplicate, invalid, contradiction or conflict (ING-03 to ING-13)? Cover: grouping rows by vote when they are not contiguous; which normalisations are allowed and how they are reported (ING-13); vote-detail conflicts (date, title, session; CON-03); idempotent re-upload of the same or an overlapping file without re-opening a pending conflict (ING-12); split votes (ING-10); whether published_at ordering matters; and the shape of the persisted import report (ING-14, ING-15).
