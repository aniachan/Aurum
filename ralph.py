#!/usr/bin/env python3
"""
Ralph Wiggum's Super Special Agent Runner! 🍰
"I'm helping!"
"""
import subprocess
import sys
import random

RALPH_QUOTES = [
    "I'm helping!",
    "Me fail English? That's unpossible!",
    "My cat's breath smells like cat food!",
    "I'm in danger!",
    "That's where I saw the leprechaun!",
    "Hi, Super Nintendo Chalmers!",
    "I bent my wookie!",
    "I'm Idaho!",
    "The doctor said I wouldn't have so many nosebleeds if I kept my finger outta there!",
    "This is my sandbox, I'm not allowed in the deep end!",
    "I dress myself!",
]

def ralph_says(quote=None):
    """Ralph says something helpful"""
    if quote:
        print(f"🍰 Ralph: {quote}")
    else:
        print(f"🍰 Ralph: {random.choice(RALPH_QUOTES)}")

def cleanup_empty_epics():
    """Close epics that have no open tasks under them"""
    ralph_says("Let me clean up the empty epics! I'm organizing!")
    
    # Get all open epics
    result = subprocess.run(
        ["bd", "list", "--type=epic", "--status=open"],
        capture_output=True,
        text=True,
        encoding='utf-8',
        errors='replace'
    )
    
    if not result.stdout or not result.stdout.strip() or "No issues" in result.stdout:
        print("No open epics found")
        return
    
    # Parse epic IDs
    epic_lines = [l for l in result.stdout.strip().split('\n') if l.strip().startswith('#')]
    
    for line in epic_lines:
        epic_id = line.split()[0].replace('#', '')
        
        # Check if epic has any open blockers (tasks under it)
        show_result = subprocess.run(
            ["bd", "show", epic_id],
            capture_output=True,
            text=True,
            encoding='utf-8',
            errors='replace'
        )
        
        # Simple heuristic: if no "Blocks:" section or it's empty, close it
        output = show_result.stdout or ""
        if "Blocks:" not in output or "Blocks: []" in output or "Blocks:\n\n" in output:
            print(f"\n📦 Closing empty epic #{epic_id}")
            subprocess.run(["bd", "close", epic_id, "--reason", "All tasks completed"])
            ralph_says("I closed an empty box!")

def run_agent_iteration(iteration_num):
    """Ralph does one task! He's helping!"""
    
    ralph_says("I'm picking a task all by myself!")
    
    # Let Ralph (opencode) pick and do the task himself
    prompt = """Follow the instructions in AGENTS.md.

Pick ONE task from 'bd ready', then work on it through to completion:
1. Run 'bd ready' to see available tasks
2. Pick one task that looks good
3. Claim it with 'bd update <id> --status in_progress'
4. Complete the work
5. Run quality gates if code changed (tests, build)
6. Close the issue with 'bd close <id>'
7. Commit all changes
8. Run 'bd sync'
9. Push to remote with 'git push'

Pick the task yourself! I'm helping!"""
    
    print(f"\n{'='*60}")
    ralph_says("Watch me code! I dress myself!")
    print(f"{'='*60}\n")
    
    result = subprocess.run(
        ["opencode", "run", prompt],
        capture_output=False
    )
    
    if result.returncode == 0:
        ralph_says("I did it! Principal Skinner is going to be so proud!")
        return True
    else:
        ralph_says("I'm in danger!")
        return False


def main():
    iterations = int(sys.argv[1]) if len(sys.argv) > 1 else 1
    
    print("\n" + "="*60)
    print("🍰  RALPH WIGGUM'S SUPER SPECIAL AGENT RUNNER  🍰")
    print("="*60)
    ralph_says("Hi, Super Nintendo Chalmers!")
    print(f"\nRalph is going to do {iterations} tasks today!\n")
    ralph_says("I'm learnding!")
    
    # Clean up empty epics first
    print("\n" + "="*60)
    print("🧹 RALPH'S CLEANUP TIME 🧹")
    print("="*60)
    cleanup_empty_epics()
    
    successful = 0
    
    for i in range(iterations):
        print(f"\n{'='*60}")
        print(f"🍰 RALPH'S TURN #{i+1} OF {iterations} 🍰")
        print(f"{'='*60}\n")
        
        success = run_agent_iteration(i+1)
        
        if not success:
            ralph_says("My brain is on fire! No wait, that's my house!")
            print(f"\n❌ Turn {i+1} didn't work or no more tasks")
            break
        
        successful += 1
        ralph_says()  # Random quote
        print(f"\n✅ Turn {i+1} complete!")
    
    print("\n" + "="*60)
    if successful == iterations:
        ralph_says("I'm helping! I did ALL the tasks!")
    elif successful > 0:
        ralph_says(f"I helped {successful} times! That's where I saw the leprechaun!")
    else:
        ralph_says("Me fail tasks? That's unpossible!")
    print("="*60 + "\n")


if __name__ == "__main__":
    main()
