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
    
    // Preload all files from IndexedDB (async, uses callback)
    balancy_indexeddb_preloadAll: function(callback, userData) {
        if (typeof BalancyIndexedDBFileHelper !== 'undefined') {
            console.log('Starting IndexedDB preload...');
            BalancyIndexedDBFileHelper.getAllFiles().then(function(files) {
                //console.log('Loaded', files.length, 'files from IndexedDB, preloading to C++...');
                var totalFiles = files.length;
                var loadedFiles = 0;
                
                // Call callback for each file
                files.forEach(function(file) {
                    var fileNameLen = lengthBytesUTF8(file.fileName) + 1;
                    var fileNamePtr = _malloc(fileNameLen);
                    stringToUTF8(file.fileName, fileNamePtr, fileNameLen);
                    
                    if (file.fileType === 'text' || typeof file.data === 'string') {
                        // Text file
                        var dataLen = lengthBytesUTF8(file.data) + 1;
                        var dataPtr = _malloc(dataLen);
                        stringToUTF8(file.data, dataPtr, dataLen);
                        var sizeToPass = dataLen - 1;
                        //console.log('[Balancy] Preloading file:', file.fileName, 'size:', sizeToPass, 'bytes (text)');
                        {{{ makeDynCall('viiii', 'callback') }}}(userData, fileNamePtr, dataPtr, sizeToPass);
                        _free(dataPtr);
                    } else if (file.data instanceof ArrayBuffer) {
                        // Binary file - allocate buffer and copy
                        var size = file.data.byteLength;
                        var buffer = _malloc(size);
                        HEAPU8.set(new Uint8Array(file.data), buffer);
                        //console.log('[Balancy] Preloading file:', file.fileName, 'size:', size, 'bytes (binary)');
                        {{{ makeDynCall('viiii', 'callback') }}}(userData, fileNamePtr, buffer, size);
                        _free(buffer);
                    }
                    
                    _free(fileNamePtr);
                    loadedFiles++;
                });
                
                console.log('✅ Preloaded', loadedFiles, 'files from IndexedDB to C++ memory cache');
                
                // Signal completion by calling with null fileName
                {{{ makeDynCall('viiii', 'callback') }}}(userData, 0, 0, -1);
            }).catch(function(error) {
                console.error('Error preloading files:', error);
                {{{ makeDynCall('viiii', 'callback') }}}(userData, 0, 0, -1);
            });
        } else {
            console.error('BalancyIndexedDBFileHelper not loaded');
            {{{ makeDynCall('viiii', 'callback') }}}(userData, 0, 0, -1);
        }
    }
});
