# Developer Notes

## Clean up filemodes
`find . -type f -not -path '*/.git' -exec chmod 644 {} +`

`find . -type d -not -path '*/.git*' -exec chmod 755 {} +`

## Github action failing with permission denied?
```
git update-index --add --chmod=+x ./create-orphan-branch.sh
```
