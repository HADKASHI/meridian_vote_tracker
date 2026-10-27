# Meridian Vote Tracker

Tracks how member states of the Meridian Assembly vote, so the Northwind Policy Institute can publish a reproducible Alignment Index.

## Assembly data

**Member state**:
A country belonging to the Assembly, identified by its ISO 3166-1 alpha-3 code, with one or more membership periods. Its identity is the code, never the name.
_Avoid_: Country (when membership matters), nation

**Session**:
The Assembly's yearly sitting, identified by a number, during which many votes are held.

**Vote**:
One resolution the Assembly voted on, with a fixed identifier (e.g. MA/2024/003), a session, a date and a title.
_Avoid_: Resolution, ballot

**Country vote**:
What one member state chose in one vote: Y (yes), N (no), A (abstain) or X (absent).
_Avoid_: Ballot (the CSV column name), row

**Vote details**:
The session, date and title of a vote.

## Ingestion

**Import**:
One upload of one file, together with its report and the outcome of every row.
_Avoid_: Upload (for the whole record), ingest

**Outcome**:
The single classification an import gives one row of a file: new, unchanged, duplicate, invalid, contradiction or conflict. It is fixed at the moment of import.
_Avoid_: Status, result

**Approved data**:
A value that was imported or that a person approved. Only approved data feeds snapshots.

**Origin**:
Where an approved value came from: either the file row that brought it in as new, or the conflict decision that approved it.
_Avoid_: Source (when meaning the whole file)

**Duplicate**:
A row identical to another row in the same file.

**Contradiction**:
Rows in the same file that disagree: one member state with different values in one vote, or different vote details for one vote.
_Avoid_: Conflict (for in-file disagreement)

**Conflict**:
An incoming value that differs from approved data already in the system, waiting for a person to accept or reject it.
_Avoid_: Contradiction (for cross-file disagreement)

## Index

**Position**:
What the Institute wanted to happen in a vote (yes or no), together with its weight.

**Weight**:
How much a vote counts in the index, from 1 (marginal) to 5 (critical).

**Alignment Index**:
The 0–100 score per member state measuring how often it voted with the Institute's positions, weighted.

**Countable vote**:
A vote in which a member state chose yes or no; only these count towards its Alignment Index. Abstentions, absences, votes outside its membership and votes with no data are not countable.

**Snapshot**:
A saved calculation of the Alignment Index over a chosen set of votes at one moment. It never changes once saved.
_Avoid_: Ranking, report

**Published snapshot**:
The one snapshot per quarter that the admin marks as the Institute's official index.

## People

**Analyst**:
A user who imports files, resolves conflicts and creates snapshots.

**Admin**:
The head of research, who sets positions and weights and marks snapshots as published.
