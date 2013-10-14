## 2013-10-11

Bugfixes:

  - write warning in logfile instead of aborting if communication to the server is not working properly
  - wait for task to start if it was unable start right away, instead of exiting with an error
  - datetime parsing if date format was in unexpected format failed for some regions
