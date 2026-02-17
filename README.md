**Mantella Skyrim character override generator/editor**

*Requirements*
* Mantella (needs to have run successfully at least once).
* xVA-Synth or xTTS (make sure all voice models are downloaded).
* A computer.


*Usage*
* On the first run you will need to select your xVA-Synth or xTTS installation directory using *File > Set xVA-Synth folder* or *File > Set xTTS folder* and locating the exectuable file.
* Use *Mode* to switch between *xVA Synth* and *xTTS*.
* Select a character from the list on the left (with some filtering options; see below), and their bio and other details will be loaded. If you've already created an override for this character the details will be pulled from there, but you can click the *Load default* button to restore the original information.
* Edit the character to your liking and click the *Save* button; this will create/update the character's Mantella override.


*Filter Options*
* **Has override?** "Yes" only shows characters that already have an override, "No" only shows characters who don't have an override, and "All" shows, er, all.
* **Voice model status** "Valid" shows characters for which you have a matching installed xVA-Synth voice model, "Invalid" shows characters for which you *don't* have a matching installed model (including ones with no model set), "None" only shows characters that don't have a voice model set, and "All" etc.
* **Voice model** This is populated from the Mantella character list, so won't neccessarily reflect the voices you actually have installed.
* **Gender** A political hot potato in these trying times, but in the context of Skyrim it's self explanatory.
* **Race** and **Species** See notes below.


*Fix invalid voice models...*

This is kind of experimental, and not particularly nuanced. Filtering the character list by "Voice model status **Invalid**" will list all characters that either have no voice model specified, or specify one you don't have installed for the current mode. You can then filter this list further with the other filter options, and then pick "Fix invalid voice models..." This attempts to find an appropriate voice model, first checking for a simple name match, then by gender/race and gender/species, then defaulting to *gender*eventoned before giving up and skipping it. If a suitable match was found it will create an override (or update an existing one) with the new model. The "Preview invalid voice model fixes..." menu option does a "dry run" and shows what changes would be made allowing you to make tweaks to characters, search for missing models, or refine your filter before committing to anything.

*Notes*
* *Species* and *Race* are generated from the character list used by Mantella, and the list seems to treat the two things very inconsistently so quite a few values appear in both lists. If anyone wants to clean that up I will award you 100 internet points. (Cue extended arguments over whether Dunmer, Bosmer, and Altmer are different *species* or different *races*.)
* Because available models vary between xVA Synth and xTTS, a valid voice model for one may not be valid for the other, so switching between modes might get you in a bit of a mess.

*To-do*
* Support for Fallout 4?
* Additional/custom field support.
* A basic report on the general state of your Mantella/xVA Synth/xTTS installations (list of missing models, a summary of how many characters are "broken", etc.)
* Smarter autofix matching (e.g. trying both "Dunmer" and "DarkElf")
* PROFIT!

*Bigups to*
[DanRuta](https://github.com/DanRuta/xVA-Synth), [Haurus](https://github.com/Haurrus/xtts-api-server-mantella/tree/local_mantella_api), and [art-from-the-machine](https://github.com/art-from-the-machine/Mantella) - you're the real MVPs.