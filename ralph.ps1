# run-ralph.ps1

$maxIterations = 50
$completionPromise = "16413cd5-514b-4bdb-b01d-e726012b4684"

$prompt = @"
On each iteration:

1. Open TASKS.md.
2. If there is at least one unchecked task:
   a. Select exactly ONE unchecked task.
   b. Implement it.
   c. Mark the task as completed in TASKS.md.
   d. Commit the change to git.
3. If there are NO unchecked tasks remaining:
   Output exactly:
   NO_UNFINISHED_TASKS_REMAIN
"@

ralph `
  --max-iterations $maxIterations `
  --completion-promise $completionPromise `
  $prompt
