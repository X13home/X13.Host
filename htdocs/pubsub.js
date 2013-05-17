var pubsub = (function () {
    "use strict";
    // private
    var STREAM_TIMEOUT = 120000,  // ms before timing out /stream request
        RETRY_DELAY = 500,      // ms delay between /stream requests

        data_cb = null,
        error_cb = null,
        pollxhr = null,
        pollxhrRepeat = false,
        timedout = false,
        streamtimer,
        pollTimer,

        isdefined = function (o) { return typeof o !== 'undefined'; },

        stop = function () {
            pollxhrRepeat = false;
            if (pollxhr !== null)
                pollxhr.abort();
        },

        reset = function () {
            stop();
        },

        pollLoop = function () {
            var i, lines, pair, topic, obj;
            pollxhr = new XMLHttpRequest();
            pollxhr.onreadystatechange = function () {
                if (pollxhr.readyState === 4) {
                    if (pollxhr.status === 200) {
                        clearTimeout(streamtimer);
                        if (null !== data_cb) {
                            lines = pollxhr.responseText.split(/\r\n/);
                            for (i = 0; i < lines.length; i++) {
                                try {
                                    obj = JSON.parse(lines[i]);
                                    for (topic in obj)
                                        if (topic!="/local/mq" && null != data_cb)
                                             data_cb(topic, obj[topic]);
                                } catch(e) {
                                    reset();
                                    if (null != error_cb)
                                        error_cb('bad json');
                                }
                            }
                        }
                        if (pollxhrRepeat)
                            pollTimer = setTimeout(function (){pollLoop();}, RETRY_DELAY);
                    }
                    else {
                        if (!timedout && pollxhrRepeat) {
                            reset();
                            if (null != error_cb)
                                error_cb(pollxhr.statusText, pollxhr.responseText);
                        }
                    }
                }
            };

            streamtimer = setTimeout(function () {
                timedout = true;
                pollxhr.abort();
                clearTimeout(pollTimer);
                timedout = false;
                if (pollxhrRepeat)
                    setTimeout(function (){pollLoop();}, RETRY_DELAY);
            }, STREAM_TIMEOUT);

            pollxhr.open('GET', '/data?read', true);
            pollxhr.send(" ");
        },

        start = function () {
            pollxhrRepeat = true;
            pollLoop();
        };

    reset();

    // public
    return {
        read : function () {
            stop();
            start();
        },

        subscribe : function (topic, cbs) {
            var req = new XMLHttpRequest();
            req.onreadystatechange = function () {
                if (req.readyState == 4) {
                    if (req.status == 200) {
                        if (isdefined(cbs) && isdefined(cbs.success))
                            cbs.success();
                    }
                    else {
                        if (isdefined(cbs) && isdefined(cbs.error))
                            cbs.error(req.statusText, req.responseText);
                        reset();
                        if (null != error_cb)
                            error_cb();
                    }
                }
            };
            req.open('POST', '/data?subscribe', true);
            req.send(topic);
        },

        publish : function (topic, msg, cbs) {
            var o = JSON.stringify(msg);
            var req = new XMLHttpRequest();
            req.onreadystatechange = function () {
                if (req.readyState == 4) {
                    if (req.status == 200) {
                        if (isdefined(cbs) && isdefined(cbs.success))
                            cbs.success(topic, msg);
                    }
                    else {
                        if (isdefined(cbs) && isdefined(cbs.error))
                            cbs.error(textStatus, errorThrown);
                        reset();
                        if (null != error_cb)
                            error_cb();
                    }
                }
            };
            req.open('POST', '/data'+topic, true);
            req.send(o);
        },

        register : function (cbs) {
            if (isdefined(cbs) && isdefined(cbs.data))
                data_cb = cbs.data;
            if (isdefined(cbs) && isdefined(cbs.error))
                error_cb = cbs.error;
        }
    }
})();