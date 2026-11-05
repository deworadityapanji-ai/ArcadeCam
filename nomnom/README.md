# NOMNOM submission

ArcadeCam.json is prepared for the v1.13.0 GitHub release. Its download URL will become valid after
that release is published with the matching ArcadeCam.dll. Do not submit the manifest before then.

1. Publish the matching source and v1.13.0 release asset in deworadityapanji-ai/ArcadeCam.
2. Verify the downloadable DLL SHA-256 matches ArcadeCam.json and its embedded version is 1.13.0.
3. Fork KopterBuzz/NOMNOM, place ArcadeCam.json in modManifests/, and submit a pull request to main.
4. NOMNOM validates the manifest; a maintainer reviews and merges it. Listing is not automatic on preparation.

The registry selects ArcadeCam.dll explicitly and limits discovery to v-prefixed releases, avoiding
the earlier 1.12 tag whose DLL version was 0.1.12. Configuration Manager is a dependency; WT Mouse Aim
is optional and is not installed as a required dependency. gameVersion 0.34.1 reflects the local game log.

Instructions: https://github.com/KopterBuzz/NOMNOM/blob/main/SCHEMA.md#how-to-contribute-mod-manifests
