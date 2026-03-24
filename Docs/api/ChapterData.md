# Eleon.Pda.ChapterData Class Reference

_Inherits Eleon.Pda.IParentLink._


## Public Member Functions

- **`ChapterData ()`**
- **`ChapterData (BinaryReader br)`**
- **`string GetChapterStartMessage ()`**
- **`void Write (BinaryWriter bw)`**
- **`void Read (BinaryReader br)`**

## Public Attributes

- **`ulong LongId`**

## Properties

- **`string ChapterTitle [get, set]`**
- **`string StartMessage [get, set]`**
- **`string Preamble [get, set]`**
- **`string Description [get, set]`**
- **`string SkipMessage [get, set]`**
- **`bool HideTasks [get, set]`**
- **`string PictureFile [get, set]`**
- **`bool NoSkip [get, set]`**
- **`float StartDelay [get, set]`**
- **`ChapterCategory Category [get, set]`**
- **`string Faction [get, set]`**
- **`string Group [get, set]`**
- **`ChapterRestriction Visibility [get, set]`**
- **`ChapterRestriction Activatable [get, set]`**
- **`List< string > PlayfieldTypes = new List<string>() [get, set]`**
- **`List< string > Playfields = new List<string>() [get, set]`**
- **`List< string > PdaReferral = new List<string>() [get, set]`**
- **`int ReputationLevel [get, set]`**
- **`RepeatData RepeatConditions [get, set]`**
- **`int PlayerCredits [get, set]`**
- **`int CoreTimer [get, set]`**
- **`int WaitForPlayers [get, set]`**
- **`List< string > VisibleOnStartPlayfields = new List<string>() [get, set]`**
- **`List< string > VisibleOnStartPlayfieldTypes = new List<string>() [get, set]`**
- **`bool HasPlayfieldRestriction [get]`**
- **`int PlayerLevel [get, set]`**
- **`List< ChapterActivationData > ChapterActivation [get, set]`**
- **`List< RewardData > Rewards [get, set]`**
- **`List< RewardData > RewardsOnSkip [get, set]`**
- **`List< string > EnableChapters [get, set]`**
- **`List< string > RewardedChapters [get, set]`**
- **`string ActivateChapterOnCompletion [get, set]`**
- **`string ActivateChapterOnSkip [get, set]`**
- **`string CompletedMessage [get, set]`**
- **`string CompletedSound [get, set]`**
- **`bool NotParallel [get, set]`**
- **`bool AutoActivateOnGameStart [get, set]`**
- **`int ActionTimeoutHours [get, set]`**
- **`string TaskUINotation [get, set]`**
- **`List< TaskData > Tasks [get, set]`**
- **`ulong Parent [get]`**
- **`ulong Parent [get]`**
