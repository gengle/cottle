using System.Collections.Generic;
using Cottle.Documents.Compiled;

namespace Cottle.Documents.Emitted.StatementGenerators
{
    internal class ForStatementGenerator : IStatementGenerator
    {
        private readonly IStatementGenerator _body;
        private readonly IStatementGenerator _empty;
        private readonly Symbol? _key;
        private readonly IExpressionGenerator _source;
        private readonly Symbol _value;

        public ForStatementGenerator(IExpressionGenerator source, Symbol? key, Symbol value, IStatementGenerator body,
            IStatementGenerator empty)
        {
            _body = body;
            _empty = empty;
            _key = key;
            _source = source;
            _value = value;
        }

        public bool Generate(Emitter emitter)
        {
            // Evaluate operand fields and store as local
            _source.Generate(emitter);

            var source = emitter.DeclareLocalAndStore<Value>();

            emitter.LoadLocalAddressAndRelease(source);
            emitter.InvokeValueFields();

            var fields = emitter.DeclareLocalAndStore<IMap>();

            // Get number of fields and jump to empty statement if count is zero
            var empty = emitter.DeclareLabel();

            emitter.LoadLocalValue(fields);
            emitter.InvokeMapCount();
            emitter.BranchIfFalse(empty);

            // Get fields enumerator and store as local
            emitter.LoadLocalValueAndRelease(fields);
            emitter.InvokeMapGetEnumerator();

            var enumerator = emitter.DeclareLocalAndStore<IEnumerator<KeyValuePair<Value, Value>>>();

            // Try moving to next element if any or terminate loop otherwise
            var exitRegular = emitter.DeclareLabel();
            var loop = emitter.DeclareLabel();

            emitter.MarkLabel(loop);
            emitter.LoadLocalValue(enumerator);
            emitter.InvokeMapEnumeratorMoveNext();
            emitter.BranchIfFalse(exitRegular);

            // Fetch current key/value pair and store as local
            emitter.LoadLocalValueAndRelease(enumerator);
            emitter.InvokeMapEnumeratorCurrent();

            var pair = emitter.DeclareLocalAndStore<KeyValuePair<Value, Value>>();

            // Set current element key if defined
            if (_key.HasValue)
            {
                emitter.LoadFrameSymbol(_key.Value);
                emitter.LoadLocalAddress(pair);
                emitter.InvokePairKey();
                emitter.StoreValueAtIndex<Value>();
            }

            // Set current element value
            emitter.LoadFrameSymbol(_value);
            emitter.LoadLocalAddressAndRelease(pair);
            emitter.InvokePairValue();
            emitter.StoreValueAtIndex<Value>();

            // Evaluate body and restart cycle
            var exitReturn = emitter.DeclareLabel();
            var mayReturn = false;

            if (_body.Generate(emitter))
            {
                emitter.LoadDuplicate();
                emitter.BranchIfTrue(exitReturn);
                emitter.Discard();

                mayReturn = true;
            }

            emitter.BranchAlways(loop);

            // Evaluate statement for "empty" case
            emitter.MarkLabel(empty);

            if (_empty != null)
            {
                if (_empty.Generate(emitter))
                {
                    emitter.LoadDuplicate();
                    emitter.BranchIfTrue(exitReturn);
                    emitter.Discard();

                    mayReturn = true;
                }

                emitter.BranchAlways(exitRegular);
            }

            // End of branch
            emitter.MarkLabel(exitRegular);

            if (mayReturn)
                emitter.LoadBoolean(false);

            // Exit statement
            emitter.MarkLabel(exitReturn);

            return mayReturn;
        }
    }
}