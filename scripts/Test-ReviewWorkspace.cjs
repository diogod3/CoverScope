const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const observers = [];
class Observer { constructor(fn) { this.fn=fn; this.disconnected=false; observers.push(this); } observe(el) {this.el=el;} disconnect(){this.disconnected=true;} }
function element() {
 const handlers = {};
 return { visible:true, scrollTop:0, scrollLeft:0, dataset:{graphWidth:'1600',graphHeight:'1200',focusX:'800',focusY:'600'}, handlers,
 getClientRects(){return this.visible ? [{}] : [];},
 getBoundingClientRect(){return {top:0,left:0,width:this.visible?600:0,height:this.visible?500:0};},
 addEventListener(name,handler,options){handlers[name]={handler,options};},
 setAttribute(name,value){this[name]=value;}, classList:{add(){},remove(){}},
 querySelectorAll(){return [{dataset:{sourceLine:10},getBoundingClientRect:()=>({top:300-this.scrollTop})}];}
 };
}
global.window={}; global.ResizeObserver=Observer;
vm.runInThisContext(fs.readFileSync('src/DD3.CoverScope.Web/wwwroot/review-workspace.js','utf8'));
const api=window.CoverScopeReview, svg=element();
api.graph.attach(svg);
const initial=svg.viewBox;
let prevented=false;
svg.handlers.wheel.handler({altKey:false,preventDefault(){prevented=true;}});
assert.equal(prevented,false); assert.equal(svg.viewBox,initial);
svg.handlers.wheel.handler({altKey:true,deltaY:100,deltaMode:0,clientX:200,clientY:100,preventDefault(){prevented=true;}});
assert.equal(prevented,true); assert.notEqual(svg.viewBox,initial);
const zoomed=svg.viewBox;
svg.visible=false; observers[0].fn(); svg.visible=true; observers[0].fn();
assert.equal(svg.viewBox,zoomed);
api.graph.detach(svg); assert.equal(observers[0].disconnected,true); assert.equal(svg.handlers.wheel.options.signal.aborted,true);
const hidden=element(); hidden.visible=false; api.graph.attach(hidden); assert.equal(hidden.viewBox,undefined);
hidden.visible=true; observers[1].fn(); assert.equal(hidden.viewBox,initial);
const source=element(); api.trackSource(source); source.scrollTop=170; source.scrollLeft=35; source.handlers.scroll.handler();
source.visible=false; observers[2].fn(); source.scrollTop=0; source.scrollLeft=0; source.handlers.scroll.handler();
assert.deepEqual(api.readSourcePosition(source),{top:170,left:35});
source.visible=true; observers[2].fn(); assert.equal(source.scrollTop,170); assert.equal(source.scrollLeft,35);
source.visible=false; observers[2].fn(); api.scrollSource(source,10);
assert.equal(api.readSourcePosition(source),null);
source.visible=true; observers[2].fn(); assert.equal(source.scrollTop,252); assert.equal(source.scrollLeft,0);
source.visible=false; observers[2].fn(); api.restoreSourcePosition(source,{top:410,left:50}); source.scrollTop=0;
source.visible=true; observers[2].fn(); assert.equal(source.scrollTop,410); assert.equal(source.scrollLeft,50);
api.untrackSource(source); assert.equal(observers[2].disconnected,true); assert.equal(source.handlers.scroll.options.signal.aborted,true);
console.log('Passed: deliberate graph zoom, hidden graph centering, camera preservation, source scroll restoration, deferred line jumps, listener cleanup.');
