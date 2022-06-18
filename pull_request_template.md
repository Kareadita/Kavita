# Added
- Added: New features 

# Changed
- Changed: Changed how something existing works (Closes #bug number)

# Fixed
- Fixed: Fixed a bug (Fixes #issue number)


# Checklist (delete section)
- Ensure your issues are not generic and instead talk about the feature or area they fix/enhance.
  - DONT: Fixed: Fixed a styling issue on top of screen
  - DO: Fixed: Fixed a styling issue on top of the book reader which caused content to be pushed down on smaller devices
- Please delete any that are not relevant. 
- You MUST use Fixed:, Changed:, Added: in front of any bullet points. 
- Do not use double quotes, use ' instead. 
- If you have not talked to me through an existing issue or in discord, leave a comment on PR with extra content. The PR description is user visible and as such, should not contain developer information.

## Example (delete section)

# Added
- Added: Added the ability to see when a scan library started by hovering over the spinner
- Added: When an OPDS collection is empty, we now send an Entry saying 'Nothing here'

# Changed
- Changed: Hashing for images now takes into account the last time it was modified, so browser doesn't cache old entries. This usually affects when files inside an archive are modified and re-read. (Fixes #631)
- Changed: + is now allowed in normalization scheme. This allows series that use + as a way to denote sequels to not merge with their prequel series. (Fixes #632)

# Fixed
- Fixed: Fixed a bug where we would reset dark mode on the book reader to dark mode if our site was on dark mode, despite user setting light mode. (Fixes #633)
