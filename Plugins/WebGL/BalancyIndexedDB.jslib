mergeInto(LibraryManager.library, {
    // Inject JavaScript code into the page
    InjectJavaScript: function(jsCodePtr) {
        var jsCode = UTF8ToString(jsCodePtr);
        var script = document.createElement('script');
        script.text = jsCode;
        document.head.appendChild(script);
        //console.log('[Balancy] JavaScript injected into page');
    },
    
    // Helper to convert C string to JS string
    balancy_js_getString: function(ptr) {
        return UTF8ToString(ptr);
    },
    
    // Helper to allocate C string from JS string
    balancy_js_allocateString: function(str) {
        var bufferSize = lengthBytesUTF8(str) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(str, buffer, bufferSize);
        return buffer;
    },
    
    // Initialize IndexedDB
    balancy_indexeddb_init: function() {
        if (typeof BalancyIndexedDBFileHelper !== 'undefined') {
            BalancyIndexedDBFileHelper.initIndexedDB().then(function() {
                //console.log('IndexedDB initialized from C++');
            }).catch(function(error) {
                console.error('Failed to initialize IndexedDB:', error);
            });
        } else {
            console.error('BalancyIndexedDBFileHelper not loaded');
        }
    },
    
    // Save text file
    balancy_indexeddb_saveFile: function(directory, fileName, data) {
        var dirStr = UTF8ToString(directory);
        var fileStr = UTF8ToString(fileName);
        var dataStr = UTF8ToString(data);
        
        if (typeof BalancyIndexedDBFileHelper !== 'undefined') {
            BalancyIndexedDBFileHelper.saveFile(dirStr, fileStr, dataStr, false).catch(function(error) {
                console.error('Error saving file:', error);
            });
        }
    },
    
    // Save binary file
    balancy_indexeddb_saveFileBinary: function(directory, fileName, dataPtr, dataSize) {
        var dirStr = UTF8ToString(directory);
        var fileStr = UTF8ToString(fileName);
        
        // Create ArrayBuffer from WASM memory
        var dataArray = new Uint8Array(HEAPU8.buffer, dataPtr, dataSize);
        var dataCopy = new Uint8Array(dataArray);
        
        if (typeof BalancyIndexedDBFileHelper !== 'undefined') {
            BalancyIndexedDBFileHelper.saveFile(dirStr, fileStr, dataCopy.buffer, true).catch(function(error) {
                console.error('Error saving binary file:', error);
            });
        }
    },
    
    // Load file (async, uses callback)
    balancy_indexeddb_loadFile: function(directory, fileName, callback, userData) {
        var dirStr = UTF8ToString(directory);
        var fileStr = UTF8ToString(fileName);
        
        if (typeof BalancyIndexedDBFileHelper !== 'undefined') {
            BalancyIndexedDBFileHelper.loadFile(dirStr, fileStr).then(function(data) {
                if (data) {
                    if (typeof data === 'string') {
                        // Text file
                        var strLen = lengthBytesUTF8(data) + 1;
                        var strPtr = _malloc(strLen);
                        stringToUTF8(data, strPtr, strLen);
                        {{{ makeDynCall('viii', 'callback') }}}(userData, strPtr, strLen - 1);
                        _free(strPtr);
                    } else if (data instanceof ArrayBuffer) {
                        // Binary file - allocate buffer and copy
                        var size = data.byteLength;
                        var buffer = _malloc(size);
                        HEAPU8.set(new Uint8Array(data), buffer);
                        {{{ makeDynCall('viii', 'callback') }}}(userData, buffer, size);
                        _free(buffer);
                    }
                } else {
                    // File not found
                    {{{ makeDynCall('viii', 'callback') }}}(userData, 0, 0);
                }
            }).catch(function(error) {
                console.error('Error loading file:', error);
                {{{ makeDynCall('viii', 'callback') }}}(userData, 0, 0);
            });
        } else {
            {{{ makeDynCall('viii', 'callback') }}}(userData, 0, 0);
        }
    },
    
    // Check if file exists (async, uses callback)
    balancy_indexeddb_fileExists: function(directory, fileName, callback, userData) {
        var dirStr = UTF8ToString(directory);
        var fileStr = UTF8ToString(fileName);
        
        if (typeof BalancyIndexedDBFileHelper !== 'undefined') {
            BalancyIndexedDBFileHelper.fileExists(dirStr, fileStr).then(function(exists) {
                {{{ makeDynCall('vii', 'callback') }}}(userData, exists ? 1 : 0);
            }).catch(function(error) {
                console.error('Error checking file existence:', error);
                {{{ makeDynCall('vii', 'callback') }}}(userData, 0);
            });
        } else {
            {{{ makeDynCall('vii', 'callback') }}}(userData, 0);
        }
    },
    
    // Delete file
    balancy_indexeddb_deleteFile: function(directory, fileName) {
        var dirStr = UTF8ToString(directory);
        var fileStr = UTF8ToString(fileName);
        
        if (typeof BalancyIndexedDBFileHelper !== 'undefined') {
            BalancyIndexedDBFileHelper.deleteFile(dirStr, fileStr).catch(function(error) {
                console.error('Error deleting file:', error);
            });
        }
    },
    
    // Clear directory
    balancy_indexeddb_clearDirectory: function(directory) {
        var dirStr = UTF8ToString(directory);
        
        if (typeof BalancyIndexedDBFileHelper !== 'undefined') {
            BalancyIndexedDBFileHelper.clearDirectory(dirStr).catch(function(error) {
                console.error('Error clearing directory:', error);
            });
        }
    },
    
    // Apply temp folder
    balancy_indexeddb_applyTempFolder: function(tempFolder) {
        var tempStr = UTF8ToString(tempFolder);
        
        if (typeof BalancyIndexedDBFileHelper !== 'undefined') {
            BalancyIndexedDBFileHelper.applyTempFolder(tempStr).then(function() {
                //console.log('Temp folder applied successfully');
            }).catch(function(error) {
                console.error('Error applying temp folder:', error);
            });
        }
    },
    
    // Get files in directory (async, uses callback)
    balancy_indexeddb_getFilesInDirectory: function(directory, callback, userData) {
        var dirStr = UTF8ToString(directory);
        
        if (typeof BalancyIndexedDBFileHelper !== 'undefined') {
            BalancyIndexedDBFileHelper.getFilesInDirectory(dirStr).then(function(files) {
                // Allocate array of string pointers
                var arraySize = files.length * 4; // 4 bytes per pointer
                var arrayPtr = _malloc(arraySize);
                
                for (var i = 0; i < files.length; i++) {
                    var strLen = lengthBytesUTF8(files[i]) + 1;
                    var strPtr = _malloc(strLen);
                    stringToUTF8(files[i], strPtr, strLen);
                    HEAP32[(arrayPtr >> 2) + i] = strPtr;
                }
                
                {{{ makeDynCall('viii', 'callback') }}}(userData, arrayPtr, files.length);
                
                // Free allocated strings
                for (var i = 0; i < files.length; i++) {
                    _free(HEAP32[(arrayPtr >> 2) + i]);
                }
                _free(arrayPtr);
            }).catch(function(error) {
                console.error('Error getting files in directory:', error);
                {{{ makeDynCall('viii', 'callback') }}}(userData, 0, 0);
            });
        } else {
            {{{ makeDynCall('viii', 'callback') }}}(userData, 0, 0);
        }
    },
    
    // Build a synchronous file index, but copy only content-read text files to WASM.
    // Binary assets remain in IndexedDB and are loaded asynchronously on demand.
    balancy_indexeddb_preloadAll: function(directory, callback, userData) {
        var directoryStr = UTF8ToString(directory);

        function isSynchronousContent(fileName) {
            var normalized = fileName.replace(/\\/g, '/');
            var parts = normalized.split('/');
            var name = parts.length ? parts[parts.length - 1] : normalized;
            if (name === 'LocalDeviceData' || name === 'user.info' || normalized.indexOf('_Profiles/') >= 0)
                return true;

            var lower = normalized.toLowerCase();
            return ['.json', '.txt', '.xml', '.csv', '.yaml', '.yml', '.js',
                    '.banim', '.html', '.css', '.lottie'].some(function(ext) {
                return lower.endsWith(ext);
            });
        }

        function notify(fileName, data) {
            var fileNameLen = lengthBytesUTF8(fileName) + 1;
            var fileNamePtr = _malloc(fileNameLen);
            stringToUTF8(fileName, fileNamePtr, fileNameLen);

            if (typeof data === 'string') {
                var dataLen = lengthBytesUTF8(data) + 1;
                var dataPtr = _malloc(dataLen);
                stringToUTF8(data, dataPtr, dataLen);
                {{{ makeDynCall('viiii', 'callback') }}}(userData, fileNamePtr, dataPtr, dataLen - 1);
                _free(dataPtr);
            } else if (data instanceof ArrayBuffer || ArrayBuffer.isView(data)) {
                var bytes = data instanceof ArrayBuffer
                    ? new Uint8Array(data)
                    : new Uint8Array(data.buffer, data.byteOffset, data.byteLength);
                var buffer = _malloc(bytes.byteLength);
                HEAPU8.set(bytes, buffer);
                {{{ makeDynCall('viiii', 'callback') }}}(userData, fileNamePtr, buffer, bytes.byteLength);
                _free(buffer);
            } else {
                // Metadata-only entry: C++ records existence without retaining content.
                {{{ makeDynCall('viiii', 'callback') }}}(userData, fileNamePtr, 0, 0);
            }

            _free(fileNamePtr);
        }

        if (typeof BalancyIndexedDBFileHelper === 'undefined') {
            console.error('BalancyIndexedDBFileHelper not loaded');
            {{{ makeDynCall('viiii', 'callback') }}}(userData, 0, 0, -1);
            return;
        }

        BalancyIndexedDBFileHelper.getAllFileNamesInDirectory(directoryStr).then(function(fileNames) {
            // The active game/branch manifest is not known during hydration. Do
            // not let an unrelated or stale combined bundle suppress a script
            // that the synchronous native core may need on this launch.
            var textFiles = fileNames.filter(isSynchronousContent);
            var textFileSet = new Set(textFiles);

            // Publish existence immediately. Text entries are overwritten with their content below.
            fileNames.forEach(function(fileName) {
                if (!textFileSet.has(fileName))
                    notify(fileName, null);
            });

            return Promise.all(textFiles.map(function(fileName) {
                return BalancyIndexedDBFileHelper.loadFile(directoryStr, fileName)
                    .then(function(data) {
                        notify(fileName, data);
                        return data ? 1 : 0;
                    })
                    .catch(function(error) {
                        console.error('Error preloading synchronous file:', fileName, error);
                        notify(fileName, null);
                        return 0;
                    });
            })).then(function(results) {
                var loadedFiles = results.reduce(function(total, value) { return total + value; }, 0);
                console.log('[Balancy] IndexedDB index ready:', fileNames.length,
                    'files;', loadedFiles, 'synchronous text files copied to WASM');
                {{{ makeDynCall('viiii', 'callback') }}}(userData, 0, 0, -1);
            });
        }).catch(function(error) {
            console.error('Error indexing IndexedDB files:', error);
            {{{ makeDynCall('viiii', 'callback') }}}(userData, 0, 0, -1);
        });
    }

});
