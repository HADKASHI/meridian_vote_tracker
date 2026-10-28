import { defineConfig } from 'orval';

// The client is generated from the committed contract, which `dotnet build` writes; never edit it by hand.
export default defineConfig({
  meridian: {
    input: '../contracts/openapi.json',
    output: {
      target: 'src/app/api/meridian.ts',
      schemas: 'src/app/api/model',
      client: 'angular',
      mode: 'tags-split',
      clean: true,
      override: {
        angular: { retrievalClient: 'both' },
      },
    },
  },
});
