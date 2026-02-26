export enum Action {
  Submenu = -1,
  /**
   * Mark entity as read
   */
  MarkAsRead = 0,
  /**
   * Mark entity as unread
   */
  MarkAsUnread = 1,
  /**
   * Invoke a Scan on Series/Library
   */
  Scan = 2,
  /**
   * Delete the entity
   */
  Delete = 3,
  /**
   * Open edit modal
   */
  Edit = 4,
  /**
   * Open details modal
   */
  Info = 5,
  /**
   * Invoke a refresh covers
   */
  RefreshMetadata = 6,
  /**
   * Download the entity
   */
  Download = 7,
  /**
   * Invoke an Analyze Files which calculates word count
   */
  AnalyzeFiles = 8,
  /**
   * Read in incognito mode aka no progress tracking
   */
  IncognitoRead = 9,
  /**
   * Add to reading list
   */
  AddToReadingList = 10,
  /**
   * Add to collection
   */
  AddToCollection = 11,
  /**
   * Open Series detail page for said series
   */
  ViewSeries = 13,
  /**
   * Open the reader for entity
   */
  Read = 14,
  /**
   * Add to user's Want to Read List
   */
  AddToWantToReadList = 15,
  /**
   * Remove from user's Want to Read List
   */
  RemoveFromWantToReadList = 16,
  /**
   * Send to a device
   */
  SendTo = 17,
  /**
   * Import some data into Kavita
   */
  Import = 18,
  /**
   * Removes the Series from On Deck inclusion
   */
  RemoveFromOnDeck = 19,
  AddRuleGroup = 20,
  RemoveRuleGroup = 21,
  MarkAsVisible = 22,
  MarkAsInvisible = 23,
  /**
   * Promotes the underlying item (Reading List, Collection)
   */
  Promote = 24,
  UnPromote = 25,
  /**
   * Invoke refresh covers as false to generate colorscapes
   */
  GenerateColorScape = 26,
  /**
   * Copy settings from one entity to another
   */
  CopySettings = 27,
  /**
   * Match an entity with an upstream system
   */
  Match = 28,
  /**
   * Merge two (or more?) entities
   */
  Merge = 29,
  /**
   * Add to a reading profile
   */
  SetReadingProfile = 30,
  /**
   * Remove the reading profile from the entity
   */
  ClearReadingProfile = 31,
  Export = 32,
  Like = 33,
  UnLike = 34,
}
