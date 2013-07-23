var pubsub = (function () {
  "use strict";
  // private
  var timer;
  var STREAM_TIMEOUT = 120000,  // ms before timing out /stream request
        RETRY_DELAY = 30,      // ms delay between /stream requests

        data_cb = null,
        error_cb = null,
        pollxhr = null,
        pollxhrRepeat = false,
        timedout = false,
        streamtimer,
        pollTimer,
        child,
        components = [],

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
                        if (topic != "/local/mq" && null != data_cb)
                          data_cb(topic, obj[topic]);
                    } catch (e) {
                      reset();
                      if (null != error_cb)
                        error_cb('bad json');
                    }
                  }
                }
                if (pollxhrRepeat)
                  pollTimer = setTimeout(function () { pollLoop(); }, RETRY_DELAY);
              } else {
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
              setTimeout(function () { pollLoop(); }, RETRY_DELAY);
          }, STREAM_TIMEOUT);

          pollxhr.open('GET', '/export?read', true);
          pollxhr.send(" ");
        },

        s4 = function () {
          return Math.floor((1 + Math.random()) * 0x10000)
             .toString(16)
             .substring(1);
        },

        guid = function () {
          return s4() + s4() + '-' + s4() + '-' + s4() + '-' +
           s4() + '-' + s4() + s4() + s4();
        },

        reconnect = function () {
          clearTimeout(timer);
          timer = setTimeout(function () { startStreaming(); }, 30);
        },

        dispatchFunc = function (topic, v) {
          var mw = $(document);
          for (var i in components) {
            if (!components.hasOwnProperty(i)) continue;
            components[i].pub(mw, topic, v);
            if (child != null) {
              components[i].pub(child, topic, v);
            }
          }
        },

        startStreaming = function () {
          var mw = $(document);
          for (var i in components) {
            if (!components.hasOwnProperty(i)) continue;
            components[i].sub(mw);
            if (child != null) {
              components[i].sub(child);
            }
          }
          pubsub.read();
          sendSubscribe("/export/+/header/+");
          sendSubscribe("/export/+/+");
        },

        sendSubscribe = function (topic, cbs) {
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
          req.open('POST', '/export?subscribe', true);
          req.send(topic);
        },

        start = function () {
          pollxhrRepeat = true;
          pollLoop();
        };

  reset();

  // public
  return {
    add: function (component) {
      components.push(component);
    },

    read: function () {
      stop();
      start();
    },

    refresh: function (frm) {
      child = frm;
      if (child != null) {
        for (var i in components) {
          if (!components.hasOwnProperty(i)) continue;
          components[i].init(child);
          components[i].sub(child);
        }
      }
    },

    subscribeEx: function (topic) {
      sendSubscribe(topic);
    },

    subscribe: function (topic, cbs) {
      if (topic.substring(0, 8) == "/export/") {
        return;
      }
      sendSubscribe(topic, cbs);
    },

    publish: function (topic, msg, cbs) {
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
      req.open('POST', topic, true);
      req.send(o);
    },

    init: function (cfg) {
      // components
      pubsub.add({    // var
        init: function (w) {
          //readonly
        },
        sub: function (w) {
          w.find("var[data-sub]").each(function (idx, el) {
            pubsub.subscribe($(el).data("sub"));
          });
        },
        pub: function (w, topic, v) {
          w.find("var[data-sub='" + topic + "']").each(function (idx, el) {
            if (msf != null && $(el).data("format") != null) {
              $(el).text(String.format($(el).data("format"), v));
            } else {
              $(el).text(v);
            }
          });
        }
      });

      pubsub.add({    // button
        init: function (w) {
          w.find("button[data-pub]").each(function (idx, el) {
            $(el).mousedown(function (obj) {
              pubsub.publish(obj.target.dataset.pub, true);
            });
            $(el).mouseup(function (obj) {
              pubsub.publish(obj.target.dataset.pub, false);
            });
          });
        },
        sub: function (w) {
          //writeonly
        },
        pub: function (w, topic, v) {
          //writeonly
        }
      });
      pubsub.add({              // checkbox
        init: function (w) {
          w.find("input[type='checkbox'][data-pub]").each(function (idx, el) {
            $(el.parentElement).addClass('checkbox');
            $(el).click(function () {
              pubsub.publish($(this).data("pub"), $(this).prop("checked"));
            });
            $.getJSON($(el).data("pub"), function (v) {
              if (v) {
                el.checked = true;
              } else {
                el.checked = false;
              }
            });
          });

          w.find("input[type='checkbox']:not([data-pub])").each(function (idx, el) {
            $(el.parentElement).addClass('checkbox').addClass("readonly");
            $(el).attr("disabled", true);
          });
        },
        sub: function (w) {
          w.find("input[type='checkbox'][data-sub]").each(function (idx, el) {
            pubsub.subscribe($(el).data("sub"));
          });
        },
        pub: function (w, topic, v) {
          w.find("input[type='checkbox'][data-sub='" + topic + "']").each(function (idx, el) {
            if (v) {
              el.checked = true;
            } else {
              el.checked = false;
            }
          });
        }
      });

      pubsub.add({    // slider
        init: function (w) {
          w.find("input[type='range'][data-pub]").each(function (idx, el) {
            $(el).change(function () {
              pubsub.publish($(this).data("pub"), $(this).val());
            });
            $.getJSON($(el).data("pub"), function (v) {
              $(el).val(v);
            });
          });

          w.find("input[type='checkbox']:not([data-pub])").each(function (idx, el) {
            $(el).attr("disabled", true);
          });
        },
        sub: function (w) {
          w.find("input[type='range'][data-sub]").each(function (idx, el) {
            pubsub.subscribe($(el).data("sub"));
          });
        },
        pub: function (w, topic, v) {
          w.find("input[type='range'][data-sub='" + topic + "']").val(v);
        }
      });

      pubsub.add({    // img
        init: function (w) {
          //readonly
        },
        sub: function (w) {
          w.find("img[data-sub]").each(function (idx, el) {
            pubsub.subscribe($(el).data("sub"));
          });
        },
        pub: function (w, topic, v) {
          w.find("img[data-sub='" + topic + "']").each(function (idx, el) {
            el.src = v;
          });
        }
      });


      //      pubsub.add({ 
      //        init: function(w){
      //        },
      //        sub: function(w){
      //        },
      //        pub: function(w, topic, v){
      //        }
      //      });

      var mw = $(document);

      for (var i in components) {
        if (!components.hasOwnProperty(i)) continue;
        components[i].init(mw);
      }

      document.cookie = "session=" + guid() + ";";
      data_cb = dispatchFunc;
      error_cb = reconnect;
      startStreaming();
      
    }
  }
})();