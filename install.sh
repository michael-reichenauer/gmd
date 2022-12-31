#!/bin/bash
 
# usage: curl -sL https://raw.githubusercontent.com/michael-reichenauer/gmd/main/install.sh | bash
# usage: curl -sL https://github.com/michael-reichenauer/gmd/releases/latest/download/install.sh | bash

echo 'Downloading gmd ...'
curl -sS -L --create-dirs -o ~/gmd/gmd "https://github.com/michael-reichenauer/gmd/releases/latest/download/gmd_linux" && chmod +x ~/gmd/gmd
if ! grep -q 'export PATH=$PATH:~/gmd' ~/.profile; then
  echo 'export PATH=$PATH:~/gmd' >>~/.profile
fi

. ~/.profile
echo "gmd version: $(gmd --version)"
echo 'To enable, run: . ~/.profile'