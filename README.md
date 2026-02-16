**Mantella/xVA-Synth Skyrim character override generator/editor**

*Requirements*
* Mantella (needs to have run successfully at least once).
* xVA-Synth (make sure all voice models are downloaded).
* A computer.

*Usage*
* On the first run you will need to select your xVA-Synth installation directory using *File > Set xVA-Synth folder*
* Select a character from the list on the left (with some filtering options to narrow the list down), and their bio and other details will be loaded. If you've already created an override for this character you can click the *Load existing override* button to load those details instead.
* Edit the character to your liking and click the *Save* button; this will create/update the character's Mantella override.

*Fix invalid voice models...*
This is kind of experimental. Filtering the character list by "Has valid voice model? **NO**" will list all characters that either have no voice model specified, or specify one you don't have installed. You can then filter this list further with the other filter options, and then pick "Fix invalid voice models..." This attempts to find an appropriate voice model, first checking for a simple name match, then by gender/race and gender/species, then defaulting to *gender*eventoned before giving up and skipping it, and creating an override with the new model (either updating an existing one if it exists, or crerating a new one). The "Preview invalid voice model fixes..." menu option does a "dry run" and shows what changes would be made allowing you to make tweaks to characters or refine your filter before committing to anything.

*To-do*
* Support for Fallout 4?
* Additional/custom field support.
* PROFIT!
