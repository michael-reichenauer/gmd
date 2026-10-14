namespace gmd.Cui.Common;

// Whether the keys typed are dropped. They are while git works (Progress), so that a key meant for
// the repo as it was shown does not act on the one being read, except in a dialog, which takes them
// while it is open, a dialog over a dialog included.
//
// A dialog used to put back, when it closed, whatever it found when it opened. One opened by the
// mouse, which is never dropped, while a read ran in the background, and closed after the read was
// done, put back 'drop every key', and the keyboard was dead until git next ran, which in an idle
// repo could be never. Whether keys are dropped is decided afresh at every change instead.
//
// The state alone, with no Terminal.Gui, so it is tested without a terminal; UI applies it.
class KeyGate
{
    bool isGitWorking;
    int openDialogs;

    public bool IsDropping => isGitWorking && openDialogs == 0;

    public void GitStarted() => isGitWorking = true;

    public void GitDone() => isGitWorking = false;

    public void DialogOpened() => openDialogs++;

    public void DialogClosed() => openDialogs = Math.Max(0, openDialogs - 1);
}
