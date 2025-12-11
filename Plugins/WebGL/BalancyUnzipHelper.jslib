/**
 * Balancy Unzip Helper for Unity WebGL
 *
 * This library handles ZIP file extraction in JavaScript using JSZip library.
 * Replicates the TypeScript SDK approach for consistency.
 */

var BalancyUnzipHelper = {
  $BalancyUnzip: {
    // Track pending unzip operations
    pendingUnzips: {},

    /**
     * Parse full path into directory and fileName for IndexedDB
     *
     * C++ calls getCachePath which returns: persistentDataPath + "/" + fileName
     * But persistentDataPath might end with underscore: "/idbfs/xxx/yyy_"
     * So the full path could be: "/idbfs/xxx/yyy_/Cache/Files/681.zip"
     *
     * We need to reconstruct the SAME directory and fileName that was used when saving.
     * When C++ called saveFileBinary(persistentDataPath, fileName), IndexedDB stored with key:
     *   getFullPath(persistentDataPath, fileName) = persistentDataPath + '/' + fileName
     *
     * So we need to split the path back into these components.
     */
    parseFilePath: function(fullPath) {
      // The path from getCachePath has pattern: <persistentDataPath>/<fileName>
      // persistentDataPath likely ends with "_" or just a GUID
      // fileName likely starts with "Cache/" or other folder names

      // Find the position where persistentDataPath ends
      // Strategy: Find the last "/" before a known top-level folder (Cache, Models, etc.)
      var knownFolders = ['Cache/', 'Models/', 'Resources/', 'Data/'];
      var bestMatch = null;

      for (var i = 0; i < knownFolders.length; i++) {
        var folderIndex = fullPath.indexOf(knownFolders[i]);
        if (folderIndex > 0) {
          // Find the "/" before this folder
          var slashIndex = fullPath.lastIndexOf('/', folderIndex - 1);
          if (slashIndex > 0) {
            bestMatch = {
              directory: fullPath.substring(0, slashIndex),
              fileName: fullPath.substring(slashIndex + 1)
            };
            break;
          }
        }
      }

      if (bestMatch) {
        console.log('[Balancy Unzip] Parsed path:', fullPath, '-> dir:', bestMatch.directory, 'file:', bestMatch.fileName);
        return bestMatch;
      }

      // Fallback: use empty directory
      console.warn('[Balancy Unzip] Could not parse path, using fallback:', fullPath);
      return {
        directory: '',
        fileName: fullPath
      };
    },

    /**
     * Get archive folder path from zip file path
     * Example: "Cache/Files/681_1762525758022.zip" -> "Cache/Files/681_1762525758022/"
     */
    getArchiveFolder: function(archivePath) {
      if (!archivePath || archivePath.length === 0) {
        return null;
      }

      // Remove .zip extension and add trailing slash
      var folderPath = archivePath.replace(/\.zip$/i, '/');
      return folderPath;
    },

    /**
     * Check if file is a text file based on extension
     */
    isTextFile: function(fileName) {
      var textExtensions = ['.html', '.css', '.js', '.json', '.txt', '.md', '.xml', '.svg'];
      var lowerFileName = fileName.toLowerCase();
      return textExtensions.some(function(ext) {
        return lowerFileName.endsWith(ext);
      });
    },

    /**
     * Load ZIP file from IndexedDB
     * This uses the direct approach - calling the BalancyIndexedDBFileHelper directly
     */
    loadZipFromIndexedDB: function(archivePath, callback) {
      console.log('[Balancy Unzip] Loading ZIP from IndexedDB:', archivePath);

      // Parse the path to extract directory and fileName
      var parsedPath = this.parseFilePath(archivePath);

      // Use BalancyIndexedDBFileHelper directly (same approach as BalancyIndexedDB.jslib)
      if (typeof BalancyIndexedDBFileHelper === 'undefined') {
        console.error('[Balancy Unzip] BalancyIndexedDBFileHelper not available!');
        callback(null);
        return;
      }

      BalancyIndexedDBFileHelper.loadFile(parsedPath.directory, parsedPath.fileName).then(function(data) {
        if (!data) {
          console.error('[Balancy Unzip] Failed to load ZIP from IndexedDB');
          callback(null);
          return;
        }

        // Data should be ArrayBuffer for ZIP files
        if (data instanceof ArrayBuffer) {
          var zipData = new Uint8Array(data);
          console.log('[Balancy Unzip] Loaded ZIP data:', zipData.length, 'bytes');
          callback(zipData);
        } else {
          console.error('[Balancy Unzip] Unexpected data type:', typeof data);
          callback(null);
        }
      }).catch(function(error) {
        console.error('[Balancy Unzip] Error loading file from IndexedDB:', error);
        callback(null);
      });
    },

    /**
     * Extract ZIP archive and save files to IndexedDB
     */
    extractZipArchive: function(id, archivePath, onComplete) {
      var self = this;

      console.log('[Balancy Unzip] Starting extraction for:', archivePath);

      // Load ZIP from IndexedDB
      this.loadZipFromIndexedDB(archivePath, function(zipData) {
        if (!zipData) {
          console.error('[Balancy Unzip] Failed to load ZIP data');
          onComplete(null);
          return;
        }

        console.log('[Balancy Unzip] Loaded ZIP archive:', zipData.length, 'bytes');

        // Get archive folder
        var archiveFolder = self.getArchiveFolder(archivePath);
        if (!archiveFolder) {
          console.error('[Balancy Unzip] Invalid archive path:', archivePath);
          onComplete(null);
          return;
        }

        console.log('[Balancy Unzip] Extracting to folder:', archiveFolder);

        // Load and extract using JSZip
        if (typeof JSZip === 'undefined') {
          console.error('[Balancy Unzip] JSZip library not loaded!');
          onComplete(null);
          return;
        }

        var zip = new JSZip();
        zip.loadAsync(zipData).then(function(zipContents) {
          console.log('[Balancy Unzip] ZIP loaded, extracting files...');

          var filePromises = [];
          var extractedCount = 0;

          // Process each file in the archive
          zipContents.forEach(function(relativePath, file) {
            // Skip directories
            if (file.dir) {
              return;
            }

            console.log('[Balancy Unzip] Extracting file:', relativePath);

            var storagePath = archiveFolder + relativePath;
            var isText = self.isTextFile(relativePath);

            var promise;
            if (isText) {
              // Extract as text
              promise = file.async('string').then(function(content) {
                return self.saveFileToIndexedDB(storagePath, content, false);
              });
            } else {
              // Extract as binary
              promise = file.async('uint8array').then(function(content) {
                return self.saveFileToIndexedDB(storagePath, content, true);
              });
            }

            promise.then(function() {
              extractedCount++;
            }).catch(function(error) {
              console.error('[Balancy Unzip] Failed to extract:', relativePath, error);
            });

            filePromises.push(promise);
          });

          // Wait for all files to be extracted
          Promise.all(filePromises).then(function() {
            console.log('[Balancy Unzip] Extraction completed. Files extracted:', extractedCount);

            // C++ expects a relative path (without the /idbfs/ prefix)
            // Extract just the relative part (e.g., "Cache/Files/681_xxx/")
            // Pattern: /idbfs/<hash>/<guid>_Cache/Files/681_xxx/
            var relativePath = archiveFolder;

            // Find where "Cache/" starts (or other known folders)
            var knownFolders = ['Cache/', 'Models/', 'Resources/', 'Data/'];
            for (var i = 0; i < knownFolders.length; i++) {
              var idx = relativePath.indexOf(knownFolders[i]);
              if (idx >= 0) {
                relativePath = relativePath.substring(idx);
                break;
              }
            }

            console.log('[Balancy Unzip] Returning relative path:', relativePath);

            // IMPORTANT: Also load the files from IndexedDB into C++ memory cache
            // so that C++'s loadFileFromCache() can find them
            self.preloadExtractedFilesIntoCppCache(archiveFolder, function() {
              console.log('[Balancy Unzip] Files preloaded into C++ memory cache');
              onComplete(relativePath);
            });

          }).catch(function(error) {
            console.error('[Balancy Unzip] Error during extraction:', error);
            onComplete(null);
          });

        }).catch(function(error) {
          console.error('[Balancy Unzip] Failed to load ZIP with JSZip:', error);
          onComplete(null);
        });
      });
    },

    /**
     * Save file to IndexedDB
     * This uses the direct approach - calling the BalancyIndexedDBFileHelper directly
     */
    saveFileToIndexedDB: function(path, data, isBinary) {
      if (typeof BalancyIndexedDBFileHelper === 'undefined') {
        console.error('[Balancy Unzip] BalancyIndexedDBFileHelper not available');
        return Promise.reject(new Error('BalancyIndexedDBFileHelper not available'));
      }

      // Convert data to appropriate format
      var dataToSave = data;
      if (isBinary && data instanceof Uint8Array) {
        // Convert Uint8Array to ArrayBuffer
        dataToSave = data.buffer;
      }

      // Save using BalancyIndexedDBFileHelper (same approach as BalancyIndexedDB.jslib)
      return BalancyIndexedDBFileHelper.saveFile('', path, dataToSave, isBinary);
    },

    /**
     * Preload extracted files from IndexedDB into C++ memory cache
     * This is necessary because C++'s loadFileFromCache only checks in-memory cache
     */
    preloadExtractedFilesIntoCppCache: function(archiveFolder, onComplete) {
      var self = this;

      // Load all files from the archive folder and send them to C++
      BalancyIndexedDBFileHelper.loadFile('', archiveFolder + 'index.html').then(function(indexHtmlData) {
        if (indexHtmlData && typeof indexHtmlData === 'string') {
          // Notify C++ to cache this file
          // We use the Unity method to save to C++ cache
          console.log('[Balancy Unzip] Preloading index.html into C++ cache, size:', indexHtmlData.length);

          // Convert string to bytes for C++
          var encoder = new TextEncoder();
          var bytes = encoder.encode(indexHtmlData);

          // Call C++ function to save in memory cache
          // We'll use the existing save mechanism
          if (typeof Module !== 'undefined' && Module.ccall) {
            try {
              // balancyPreloadFileFromStreamingAssets(fileName, fileData, dataSize)
              var fileNameWithPath = archiveFolder.replace(/^\/idbfs\/[^\/]+\/[^\/]+_/, '') + 'index.html';

              Module.ccall('balancyPreloadFileFromStreamingAssets',
                'void',
                ['string', 'array', 'number'],
                [fileNameWithPath, bytes, bytes.length]);
            } catch (e) {
              console.error('[Balancy Unzip] Failed to preload to C++:', e);
            }
          }
        }

        // Also load manifest.json if it exists
        return BalancyIndexedDBFileHelper.loadFile('', archiveFolder + 'manifest.json');
      }).then(function(manifestData) {
        if (manifestData && typeof manifestData === 'string') {
          console.log('[Balancy Unzip] Preloading manifest.json into C++ cache, size:', manifestData.length);

          var encoder = new TextEncoder();
          var bytes = encoder.encode(manifestData);

          if (typeof Module !== 'undefined' && Module.ccall) {
            try {
              var fileNameWithPath = archiveFolder.replace(/^\/idbfs\/[^\/]+\/[^\/]+_/, '') + 'manifest.json';

              Module.ccall('balancyPreloadFileFromStreamingAssets',
                'void',
                ['string', 'array', 'number'],
                [fileNameWithPath, bytes, bytes.length]);
            } catch (e) {
              console.error('[Balancy Unzip] Failed to preload manifest to C++:', e);
            }
          }
        }

        onComplete();
      }).catch(function(error) {
        console.error('[Balancy Unzip] Error preloading files:', error);
        onComplete(); // Continue anyway
      });
    }
  },

  /**
   * Main unzip function called from C#
   * @param id - Unzip request ID
   * @param zipFilePath - Path to ZIP file in IndexedDB
   */
  BalancyUnzipFile: function(idPtr, zipFilePathPtr) {
    var id = UTF8ToString(idPtr);
    var zipFilePath = UTF8ToString(zipFilePathPtr);

    console.log('[Balancy Unzip] Unzip request:', id, zipFilePath);

    // Extract the archive
    BalancyUnzip.extractZipArchive(id, zipFilePath, function(resultPath) {
      // Notify C# of completion
      if (resultPath) {
        console.log('[Balancy Unzip] ✅ Success! Notifying Unity:', resultPath);

        // Call Unity's balancyUnzipCompleted via the existing callback
        // The C# UnzipBridge has already registered this callback
        if (typeof Module.BalancyUnzipCompleted !== 'undefined') {
          Module.BalancyUnzipCompleted(id, resultPath);
        } else {
          console.error('[Balancy Unzip] Unity callback not registered!');
        }
      } else {
        console.error('[Balancy Unzip] ❌ Failed! Notifying Unity with empty path');

        if (typeof Module.BalancyUnzipCompleted !== 'undefined') {
          Module.BalancyUnzipCompleted(id, '');
        }
      }
    });
  }
};

// Register the library
autoAddDeps(BalancyUnzipHelper, '$BalancyUnzip');
mergeInto(LibraryManager.library, BalancyUnzipHelper);
