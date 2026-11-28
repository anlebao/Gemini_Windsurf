#!/bin/bash
sudo docker exec vanan-crawler-1 sh -c "WL='-c tessedit_char_whitelist=0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz'; for P in 7 8 13; do echo -n \"psm\$P:\"; tesseract /tmp/cap.png stdout --psm \$P -l eng \$WL 2>/dev/null | tr -cd '[:alnum:]'; echo \"\"; done"
