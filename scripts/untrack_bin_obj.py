import subprocess, sys, os

os.chdir(r'C:\VibeCoding\Gemini_Windsurf')

# Lấy toàn bộ tracked files chứa /bin/ hoặc /obj/
result = subprocess.run(
    ['git', 'ls-files', '-z'],
    capture_output=True
)
all_files = result.stdout.decode('utf-8').split('\0')
bin_obj = [f for f in all_files if f and ('/bin/' in f or '/obj/' in f)]
print(f'Found {len(bin_obj)} tracked bin/obj files. Untracking...')

# Batch git rm --cached theo nhóm 50
BATCH = 50
errors = 0
for i in range(0, len(bin_obj), BATCH):
    batch = bin_obj[i:i+BATCH]
    res = subprocess.run(
        ['git', 'rm', '--cached', '--quiet', '--ignore-unmatch'] + batch,
        capture_output=True, text=True
    )
    if res.returncode != 0:
        errors += 1
        print(f'  BATCH {i//BATCH} error: {res.stderr[:100]}')
    else:
        print(f'  Batch {i//BATCH+1}: {len(batch)} files removed')

print(f'\nDone. {len(bin_obj)} files untracked, {errors} batch errors.')
