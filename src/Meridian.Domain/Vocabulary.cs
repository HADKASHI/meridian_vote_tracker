namespace Meridian.Domain;

// The glossary's fixed value lists. Each is stored as its snake_case name, and the
// integrity ones are also checked by the database (docs/schema.md).

/// <summary>The single classification an import gives one row of a file.</summary>
public enum Outcome { New, Unchanged, Duplicate, Invalid, Contradiction, Conflict }

/// <summary>What one member state chose in one vote: yes, no, abstain or absent.</summary>
public enum Ballot { Y, N, A, X }

/// <summary>What the Institute wanted to happen in a vote.</summary>
public enum Stance { Yes, No }

public enum ConflictSubject { Ballot, Details }

public enum Decision { Accept, Reject }

/// <summary>How a snapshot treated one member state's country vote in one vote.</summary>
public enum LineCategory { Match, Mismatch, Abstain, Absent, NonMember, NoData }

public enum Role { Analyst, Admin }

public enum JobKind { Import, Snapshot }

/// <summary>A job is queued, running, done or failed. Done means the work finished.</summary>
public enum JobState { Queued, Running, Done, Failed }
