#!/bin/bash
# Author:Andrey Nikishaev

echo "CHANGELOG"
echo ----------------------
git tag -l | sort -u -r -V | while read TAG ; do
    echo
    if [ $NEXT ];then
        echo [$NEXT]
    else
        echo "[Current]"
    fi
    GIT_PAGER=cat git log main --first-parent --format=" * %s" $TAG..$NEXT
    NEXT=$TAG
done
# FIRST=$(git tag -l | head -1)
# echo
# echo [$FIRST]
# GIT_PAGER=cat git log --no-merges --format=" * %s" $FIRST