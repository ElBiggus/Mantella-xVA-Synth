**Mantella Skyrim character override generator/editor**

*Requirements*
* Mantella (needs to have run successfully at least once).
* xVA-Synth or xTTS (make sure all voice models are downloaded).
* A computer.


*Basic Usage*
* On the first run you will need to set your xVA-Synth or xTTS installation directory; from the *File* menu select *Set xVA-Synth directory* or *Select xTTS directory* and locate the exectuable file in the directory it's installed in.
* Use the *Mode* to switch between *xVA Synth* and *xTTS* modes.
* Select a character from the list on the left and their bio and other details will be loaded into the editing area. If you've already created an override for this character the details will be pulled from there instead of the original source, but you can click the *Load default* button to restore the original information.
* Edit the character to your liking and click the *Save* button; this will create/update the character's Mantella override.


*Filter Options*

You can filter the list of displayed characters to narrow down the list. (These filters also affect the scope of the "Fix invalid voice models" functionality.)
* **Has override?** "Yes" only shows characters that already have an override, "No" only shows characters who don't have an override, and "All" shows, er, all.
* **Voice model status** "Valid" shows characters for which you have a matching installed xVA-Synth or xTTS voice model (depending on what mode you're in), "Invalid" shows characters for which you *don't* have a matching installed model (including ones with no model set), "None" only shows characters that don't have a voice model set, and "All" etc.
* **Voice model** This is populated from the Mantella character list, so won't neccessarily reflect the voices you actually have installed.
* **Gender** A political hot potato in these trying times, but in the context of Skyrim it's self explanatory.
* **Race** and **Species** See notes below.
* **Name** I have no idea what this does. It's a mystery for the ages!


*Fix invalid voice models...*

This is kind of experimental, and using it without care could end up with some unexpected results! It attempts to use character name, gender, race, and species to select an appropriate voice model (from those you have installed) for characters that don't have a valid one set. How well this works depends on a lot of factors so I would recommend setting filter options to limit it to a few entries at a time and using *Preview invalid voice model fixes...* to check what it's going to do before committing.

*Fix invalid voice models (Fuzzy)...*

This includes some additional logic that may improve the results. (On the other hand it may not; again, use the preview option first!)

*Notes*
* *Species* and *Race* are generated from the character list used by Mantella, and the list seems to treat the two things very inconsistently so quite a few values appear in both lists. If anyone wants to clean that up I will award you 100 internet points. (Cue extended arguments over whether Dunmer, Bosmer, and Altmer are different *species* or different *races*.)
* Because available models vary between xVA Synth and xTTS, a valid voice model for one may not be valid for the other, so switching between modes might get you in a bit of a mess.

*To-do*
* Support for Fallout 4?
* Additional/custom field support.
* A basic report on the general state of your Mantella/xVA Synth/xTTS installations (list of missing models, a summary of how many characters are "broken", etc.)
* PROFIT!

*Bigups to*
[DanRuta](https://github.com/DanRuta/xVA-Synth), [Haurus](https://github.com/Haurrus/xtts-api-server-mantella/tree/local_mantella_api), and [art-from-the-machine](https://github.com/art-from-the-machine/Mantella) - you're the real MVPs.