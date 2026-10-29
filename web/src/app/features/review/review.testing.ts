import { ConflictDto, ConflictListItemDto, OriginDto, PageDtoOfConflictListItemDto } from '../../api/model';

// Response bodies for the review specs.

export function listItem(overrides: Partial<ConflictListItemDto> = {}): ConflictListItemDto {
  return {
    kind: 'conflict', id: 7, vote: { voteId: 101, assemblyVoteId: 'MA/2024/101' }, subject: 'ballot', stateCode: 'BRA',
    current: { ballot: 'y', session: null, voteDate: null, title: null },
    incoming: { ballot: 'a', session: null, voteDate: null, title: null },
    importId: 2, openedAt: '2024-04-02T10:00:00Z', decision: null,
    ...overrides,
  };
}

export function page(items: ConflictListItemDto[]): PageDtoOfConflictListItemDto {
  return { items, total: items.length, page: 1, pageSize: 50 };
}

export function importRowOrigin(overrides: Partial<OriginDto> = {}): OriginDto {
  return {
    kind: 'import_row', importId: 1, fileName: 'q1.csv', lineNo: 12, importRowId: 40, uploadedBy: 'dana',
    uploadedAt: '2024-04-01T09:00:00Z', decisionId: null, conflictId: null, decidedBy: null, decidedAt: null,
    ...overrides,
  };
}

export function conflict(overrides: Partial<ConflictDto> = {}): ConflictDto {
  return {
    id: 7, vote: { voteId: 101, assemblyVoteId: 'MA/2024/101' }, subject: 'ballot', stateCode: 'BRA',
    current: { value: { ballot: 'y', session: null, voteDate: null, title: null }, revisionId: 30, origin: importRowOrigin() },
    incoming: {
      value: { ballot: 'a', session: null, voteDate: null, title: null }, revisionId: null,
      origin: importRowOrigin({ importId: 2, fileName: 'q1-rev2.csv', lineNo: 88, importRowId: 90, uploadedBy: 'omar' }),
    },
    comparedRevisionId: 30, decision: null, notApplied: [],
    ...overrides,
  };
}
