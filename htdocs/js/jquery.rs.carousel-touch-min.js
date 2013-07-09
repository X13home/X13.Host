/*! jquery.rs.carousel-min.js | 1.0.1 | 2013-04-30 | http://richardscarrott.github.com/jquery-ui-carousel/ */
(function(t){"use strict";var e=t.Widget.prototype;t.widget("rs.draggable3d",{options:{axis:"x",translate3d:!1},_create:function(){this.eventNamespace=this.eventNamespace||"."+this.widgetName,this._bindDragEvents()},_bindDragEvents:function(){var t=this,e=this.eventNamespace;this.element.unbind(e).bind("dragstart"+e,{axis:this.options.axis},function(e){t._start(e)}).bind("drag"+e,function(e){t._drag(e)}).bind("dragend"+e,function(e){t._end(e)})},_getPosStr:function(){return"x"===this.options.axis?"left":"top"},_start:function(t){this.mouseStartPos="x"===this.options.axis?t.pageX:t.pageY,this.elPos=this.options.translate3d?this.element.css("translate3d")[this.options.axis]:parseInt(this.element.position()[this._getPosStr()],10),this._trigger("start",t)},_drag:function(t){var e="x"===this.options.axis?t.pageX:t.pageY,i=e-this.mouseStartPos+this.elPos,s={};this.options.translate3d?s.translate3d="x"===this.options.axis?{x:i}:{y:i}:s[this._getPosStr()]=i,this.element.css(s)},_end:function(t){this._trigger("stop",t)},_setOption:function(t){e._setOption.apply(this,arguments),"axis"===t&&this._bindDragEvents()},destroy:function(){var t={};this.options.translate3d?t.translate3d={}:t[this._getPosStr()]="",this.element.css(t),e.destroy.apply(this)}})})(jQuery),function(t){"use strict";var e=t.rs.carousel.prototype;t.widget("rs.carousel",t.rs.carousel,{options:{touch:!1,sensitivity:1},_create:function(){e._create.apply(this),this._initDrag()},_initDrag:function(){var t=this;this.elements.runner.draggable3d({translate3d:this.options.translate3d,axis:this._getAxis(),start:function(e){e=e.originalEvent.touches?e.originalEvent.touches[0]:e,t._dragStartHandler(e)},stop:function(e){e=e.originalEvent.touches?e.originalEvent.touches[0]:e,t._dragStopHandler(e)}})},_destroyDrag:function(){this.elements.runner.draggable3d("destroy"),this.goToPage(this.index,!1,void 0,!0)},_getAxis:function(){return this.isHorizontal?"x":"y"},_dragStartHandler:function(t){this.options.translate3d&&this.elements.runner.removeClass(this.widgetFullName+"-runner-transition"),this.startTime=this._getTime(),this.startPos={x:t.pageX,y:t.pageY}},_dragStopHandler:function(t){var e,i,s,n,o=this._getAxis();this.endTime=this._getTime(),e=this.endTime-this.startTime,this.endPos={x:t.pageX,y:t.pageY},i=Math.abs(this.startPos[o]-this.endPos[o]),s=i/e,n=this.startPos[o]>this.endPos[o]?"next":"prev",s>this.options.sensitivity||i>this._getMaskDim()/2?this.index===this.getNoOfPages()-1&&"next"===n||0===this.index&&"prev"===n?this.goToPage(this.index):this[n]():this.goToPage(this.index)},_getTime:function(){var t=new Date;return t.getTime()},_setOption:function(t,i){switch(e._setOption.apply(this,arguments),t){case"orientation":this._switchAxis();break;case"touch":i?this._initDrag():this._destroyDrag()}},_switchAxis:function(){this.elements.runner.draggable3d("option","axis",this._getAxis())},destroy:function(){this._destroyDrag(),e.destroy.apply(this)}})}(jQuery);