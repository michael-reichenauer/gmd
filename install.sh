#!/bin/bash
 
echo 'Downloading gmd ...'
curl -sS -L --create-dirs -o ~/gmd/gmd "https://github.com/michael-reichenauer/gmd/releases/latest/download/gmd_linux" && chmod +x ~/gmd/gmd
if ! grep -q 'export PATH=$PATH:~/gmd' ~/.profile; then
  echo 'export PATH=$PATH:~/gmd' >>~/.profile
fi

. ~/.profile
echo "gmd version: $(gmd --version)"
echo 'To enable, run: . ~/.profile'